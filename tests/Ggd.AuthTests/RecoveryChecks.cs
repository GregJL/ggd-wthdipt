using System.Net.Http.Json;
using System.Text.Json;
using Ggd.Api.Data;
using Ggd.Api.Models;
using Ggd.Api.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ggd.AuthTests;

internal static class RecoveryChecks
{
    public static async Task RunAsync()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        connection.CreateFunction<string?, string?, int>("num_nonnulls", (a, b) => (a is null ? 0 : 1) + (b is null ? 0 : 1));
        var mail = new TestMail(); var clock = new TestClock();
        using var factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder => {
            builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../server")));
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                ["AccountSetup:Code"] = "test-recovery-setup-code-not-real-1234567890",
                ["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), "ggd-recovery-tests-" + Guid.NewGuid()),
                ["Logging:LogLevel:Default"] = "Error"
            }));
            builder.ConfigureServices(services => {
                services.RemoveAll<DbContextOptions<InventoryDbContext>>(); services.RemoveAll<IDbContextOptionsConfiguration<InventoryDbContext>>();
                services.AddDbContext<InventoryDbContext>(options => options.UseSqlite(connection));
                services.RemoveAll<IRecoveryEmail>(); services.AddSingleton<IRecoveryEmail>(mail);
                services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
            });
        });
        var settings = new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false };
        using var owner = factory.CreateClient(settings);
        using (var scope = factory.Services.CreateScope()) { await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.EnsureCreatedAsync(); }
        await DbSeeder.SeedAsync(factory.Services);
        await Token(owner);
        await Status(await owner.PostAsJsonAsync("/api/auth/setup", new { email = "greg@example.test", displayName = "Greg", password = "Old test passphrase 123!", setupCode = "test-recovery-setup-code-not-real-1234567890" }), 204);
        await Token(owner);
        using var client = factory.CreateClient(settings); await Token(client);
        var unknown = await client.PostAsJsonAsync("/api/auth/recovery/request", new { email = "absent@example.test" }); await Status(unknown, 200);
        var known = await client.PostAsJsonAsync("/api/auth/recovery/request", new { email = "GREG@example.test", deliveryAddress = "attacker@example.test" }); await Status(known, 200);
        Check(await unknown.Content.ReadAsStringAsync() == await known.Content.ReadAsStringAsync(), "generic known and unknown account responses");
        Check(mail.Sent.Count == 1 && mail.Sent[0].Address == "greg@example.test", "only address on file receives code");
        var first = mail.Sent[0].Code;
        Check(first.Length == 8 && first.All(char.IsDigit), "eight-digit code");
        await Status(await client.PostAsJsonAsync("/api/auth/recovery/request", new { email = "greg@example.test" }), 200);
        Check(mail.Sent.Count == 1, "resend cooldown");
        using (var scope = factory.Services.CreateScope()) {
            var challenge = await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().PasswordRecoveries.SingleAsync();
            Check(!challenge.ProtectedCodeHash.Contains(first), "no plaintext code in database");
        }
        await Reset("not-code", 400);
        clock.Advance(TimeSpan.FromMinutes(11)); await Reset(first, 400);
        await Status(await client.PostAsJsonAsync("/api/auth/recovery/request", new { email = "greg@example.test" }), 200);
        var second = mail.Sent[^1].Code;
        for (var i = 0; i < 5; i++) await Reset("not-code", 400);
        await Reset(second, 400); // Exhausted attempts reject even the right code.
        clock.Advance(TimeSpan.FromMinutes(2));
        await Status(await client.PostAsJsonAsync("/api/auth/recovery/request", new { email = "greg@example.test" }), 200);
        var final = mail.Sent[^1].Code;
        using (var scope = factory.Services.CreateScope()) {
            var recovery = scope.ServiceProvider.GetRequiredService<PasswordRecoveryService>();
            Check(await recovery.ResetAsync("greg@example.test", final, "short", default) is not null, "weak new password denied");
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(); var user = (await users.FindByIdAsync(DbSeeder.GregUserId.ToString()))!;
            await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(15));
        }
        await Reset(final, 204); await Reset(final, 400); // One-time use.
        await Status(await owner.GetAsync($"/api/items?workspaceId={DbSeeder.WorkspaceId}"), 401);
        using (var scope = factory.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(); var user = (await users.FindByIdAsync(DbSeeder.GregUserId.ToString()))!;
            Check(await users.CheckPasswordAsync(user, "New test passphrase 456!"), "new password works");
            Check(!await users.CheckPasswordAsync(user, "Old test passphrase 123!"), "old password revoked");
            Check(user.LockoutEnd is null && user.AccessFailedCount == 0, "reset clears lockout");
            Check(!await db.PasswordRecoveries.AnyAsync(), "challenge consumed");
            Check(await db.Locations.AnyAsync() && await db.WorkspaceMembers.AnyAsync(x => x.UserId == user.Id), "inventory and identity retained");
        }
        Console.WriteLine("PASS: recovery email target, generic responses, cooldown, protected code, expiry, attempt cap, password validation, one-time reset, session invalidation, and account/data preservation.");
        async Task Reset(string code, int expected) => await Status(await client.PostAsJsonAsync("/api/auth/recovery/reset", new { email = "greg@example.test", code, newPassword = "New test passphrase 456!" }), expected);
    }
    private static async Task Token(HttpClient client) {
        var session = await client.GetFromJsonAsync<JsonElement>("/api/auth/session");
        client.DefaultRequestHeaders.Remove("X-GGD-CSRF"); client.DefaultRequestHeaders.Add("X-GGD-CSRF", session.GetProperty("csrfToken").GetString());
    }
    private static void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); }
    private static async Task Status(HttpResponseMessage response, int expected) { Check((int)response.StatusCode == expected, $"HTTP {expected} expected; got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}"); }
    private sealed class TestMail : IRecoveryEmail {
        public bool IsConfigured => true;
        public List<(string Address, string Code)> Sent { get; } = [];
        public Task SendAsync(string addressOnFile, string code, CancellationToken cancellationToken) { Sent.Add((addressOnFile, code)); return Task.CompletedTask; }
    }
    private sealed class TestClock : TimeProvider {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }
}
