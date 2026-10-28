using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ggd.Api.Data;
using Ggd.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ggd.AuthTests;

internal static class AuthChecks
{
    private const string Code = "test-only-setup-code-not-a-real-secret-123456789";
    private const string Password = "Test only passphrase 123!";
    public static async Task Main()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateFunction<string?, string?, int>("num_nonnulls", (a, b) => (a is null ? 0 : 1) + (b is null ? 0 : 1));
        using var factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../server")));
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                ["AccountSetup:Code"] = Code,
                ["Logging:LogLevel:Default"] = "Warning",
                ["PhotoStorage:Path"] = Path.Combine(Path.GetTempPath(), "ggd-auth-photo-tests-" + Guid.NewGuid()),
                ["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), "ggd-auth-tests-" + Guid.NewGuid())
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<InventoryDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<InventoryDbContext>>();
                services.AddDbContext<InventoryDbContext>(options => options.UseSqlite(connection));
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await db.Database.EnsureCreatedAsync();
            await DbSeeder.SeedAsync(factory.Services);
        }
        var workspace = DbSeeder.WorkspaceId;
        var locationId = Guid.Parse("00000000-0000-0000-0000-000000000301");
        await Status(await client.GetAsync($"/api/items?workspaceId={workspace}"), 401, "anonymous inventory denied");
        await Status(await client.GetAsync($"/api/photos/{Guid.NewGuid()}/content?workspaceId={workspace}"), 401, "anonymous photos denied");
        var session = await Session(client);
        Check(session.GetProperty("setupRequired").GetBoolean(), "setup required");
        await Status(await client.PostAsJsonAsync("/api/auth/setup", new { email = "greg@example.test", displayName = "Greg", password = Password, setupCode = Code }), 400, "setup needs CSRF");
        Token(client, session);
        await Status(await client.PostAsJsonAsync("/api/auth/setup", new { email = "greg@example.test", displayName = "Greg", password = Password, setupCode = "wrong" }), 400, "setup needs private code");
        await Status(await client.PostAsJsonAsync("/api/auth/setup", new { email = "greg@example.test", displayName = "Greg", password = "short", setupCode = Code }), 400, "weak password rejected");
        await Status(await client.PostAsJsonAsync("/api/auth/setup", new { email = "greg@example.test", displayName = "Greg", password = Password, setupCode = Code }), 204, "setup succeeds");
        session = await Session(client); Token(client, session);
        Check(session.GetProperty("user").GetProperty("id").GetGuid() == DbSeeder.GregUserId, "owner identity preserved");
        Check(!session.GetProperty("setupRequired").GetBoolean(), "setup disabled");
        await Status(await client.PostAsJsonAsync("/api/auth/setup", new { email = "other@example.test", displayName = "Other", password = Password, setupCode = Code }), 409, "cannot claim a second account");
        await Status(await client.GetAsync($"/api/locations/all?workspaceId={workspace}"), 200, "existing inventory accessible");
        await Status(await client.GetAsync($"/api/items?workspaceId={Guid.NewGuid()}"), 403, "other household denied");
        await Status(await client.PostAsJsonAsync("/api/items", new { workspaceId = Guid.NewGuid(), locationId, name = "Denied", quantity = 1 }), 403, "foreign body denied");
        await Status(await client.PostAsJsonAsync($"/api/items?workspaceId={workspace}", new { workspaceId = Guid.NewGuid(), locationId, name = "Denied", quantity = 1 }), 403, "query/body mismatch denied");
        var duplicateCase = "{\"workspaceId\":\"" + workspace + "\",\"WorkspaceId\":\"" + Guid.NewGuid() + "\",\"name\":\"Denied\",\"quantity\":1,\"locationId\":\"" + locationId + "\"}";
        await Status(await client.PostAsync("/api/items", new StringContent(duplicateCase, Encoding.UTF8, "application/json")), 403, "case-variant workspace spoof denied");
        client.DefaultRequestHeaders.Remove("X-GGD-CSRF");
        await Status(await client.PostAsJsonAsync("/api/items", new { workspaceId = workspace, locationId, name = "Denied", quantity = 1 }), 400, "inventory mutation needs CSRF");
        Token(client, session);
        var created = await client.PostAsJsonAsync("/api/items", new { workspaceId = workspace, locationId, name = "Account test drill", quantity = 1, actorName = "Spoof" });
        await Status(created, 201, "authenticated create");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var latest = await db.ActivityEntries.OrderByDescending(x => x.Id).FirstAsync();
            Check(latest.ActorUserId == DbSeeder.GregUserId && latest.ActorName == "Greg", "actor comes from server");
            Check(await db.ActivityEntries.AnyAsync(x => x.ActorUserId == null && x.ActorName == null), "legacy attribution untouched");
            var user = await db.Users.SingleAsync();
            Check(user.LockoutEnabled, "lockout enabled for existing owner");
            Check(user.PasswordHash is not null && user.PasswordHash != Password, "password hashed");
        }
        await ItemDetailsChecks.RunAsync(client, workspace, locationId);
        await OrganizationChecks.RunAsync(client, workspace, locationId, factory.Services);
        using (var form = new MultipartFormDataContent())
        {
            form.Add(new StringContent(workspace.ToString()), "workspaceId");
            form.Add(new StringContent(locationId.ToString()), "locationId");
            var file = new ByteArrayContent([0xff, 0xd8, 0xff, 0xd9]);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            form.Add(file, "file", "test.jpg");
            var uploaded = await client.PostAsync("/api/photos", form);
            await Status(uploaded, 201, "authenticated multipart upload");
            var photo = JsonSerializer.Deserialize<JsonElement>(await uploaded.Content.ReadAsStringAsync());
            await Status(await client.GetAsync(photo.GetProperty("contentUrl").GetString()), 200, "signed-in photo access");
        }
        await Status(await client.PostAsJsonAsync("/api/auth/register", new { email = "other@example.test", password = Password }), 404, "public registration absent");
        await Status(await client.PostAsJsonAsync("/api/auth/password", new { currentPassword = "Wrong old password", newPassword = "Replacement passphrase 456!" }), 400, "password change needs old password");
        await Status(await client.PostAsJsonAsync("/api/auth/password", new { currentPassword = Password, newPassword = "Replacement passphrase 456!" }), 204, "password change");
        session = await Session(client); Token(client, session);
        await Status(await client.PostAsync("/api/auth/logout", null), 204, "logout");
        await Status(await client.GetAsync($"/api/activity?workspaceId={workspace}"), 401, "history denied after logout");
        session = await Session(client); Token(client, session);
        await Status(await client.PostAsJsonAsync("/api/auth/login", new { email = "greg@example.test", password = Password, rememberMe = true }), 401, "old password denied");
        await Status(await client.PostAsJsonAsync("/api/auth/login", new { email = "GREG@example.test", password = "Replacement passphrase 456!", rememberMe = true }), 204, "new password and normalized email sign in");
        session = await Session(client); Token(client, session);
        await Status(await client.PostAsync("/api/auth/logout", null), 204, "logout before lockout check");
        session = await Session(client); Token(client, session);
        for (var failure = 0; failure < 5; failure++)
            await Status(await client.PostAsJsonAsync("/api/auth/login", new { email = "greg@example.test", password = "Wrong test password", rememberMe = true }), 401, "failed login");
        await Status(await client.PostAsJsonAsync("/api/auth/login", new { email = "greg@example.test", password = "Replacement passphrase 456!", rememberMe = true }), 401, "locked account cannot sign in");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            Check((await db.Users.SingleAsync()).LockoutEnd > DateTimeOffset.UtcNow, "failed attempts activate lockout");
        }
        await RecoveryChecks.RunAsync();
        Console.WriteLine("PASS: setup code, one-time claim, owner preservation, password hashing/change, cookies, CSRF, household isolation, spoof prevention, actor attribution, legacy history, photo access, login/logout, and lockout.");
    }
    private static void Token(HttpClient client, JsonElement session)
    { client.DefaultRequestHeaders.Remove("X-GGD-CSRF"); client.DefaultRequestHeaders.Add("X-GGD-CSRF", session.GetProperty("csrfToken").GetString()); }
    private static async Task<JsonElement> Session(HttpClient client) => JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync("/api/auth/session"));
    private static void Check(bool condition, string message) { if (!condition) throw new Exception("FAIL: " + message); }
    private static async Task Status(HttpResponseMessage response, int expected, string message)
    { Check((int)response.StatusCode == expected, $"{message}: expected {expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}"); }
}
