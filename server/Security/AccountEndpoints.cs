using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Ggd.Api.Data;
using Ggd.Api.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ggd.Api.Security;

public static class AccountEndpoints
{
    private static readonly SemaphoreSlim SetupGate = new(1, 1);

    public static void MapAccountEndpoints(this WebApplication app)
    {
        var account = app.MapGroup("/api/auth");
        account.MapGet("/session", async (HttpContext context, InventoryDbContext db, UserManager<AppUser> users, IAntiforgery antiforgery, IConfiguration configuration, IRecoveryEmail recoveryEmail) =>
        {
            var user = await users.GetUserAsync(context.User);
            var setupRequired = !await db.Users.AnyAsync(x => x.PasswordHash != null);
            var member = user is null ? null : await db.WorkspaceMembers.AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == user.Id && x.WorkspaceId == DbSeeder.WorkspaceId);
            return Results.Ok(new {
                user = user is not null && member is not null ? new { user.Id, user.Email, user.DisplayName, member.WorkspaceId } : null,
                recoveryAvailable = recoveryEmail.IsConfigured,
                setupRequired,
                setupAvailable = setupRequired && configuration["AccountSetup:Code"] is { Length: >= 32 },
                csrfToken = antiforgery.GetAndStoreTokens(context).RequestToken
            });
        });
        account.MapPost("/recovery/request", async (RecoveryCodeRequest request, IRecoveryEmail mail, PasswordRecoveryService recovery, HttpContext context) =>
        {
            if (!mail.IsConfigured) return Results.Text("Recovery email needs to be configured on the server PC.", statusCode: 503);
            if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256) return Results.BadRequest("Enter your account email.");
            await recovery.RequestAsync(request.Email, context.RequestAborted);
            return Results.Ok(new { message = PasswordRecoveryService.RequestMessage });
        }).RequireRateLimiting("recovery-request");
        account.MapPost("/recovery/reset", async (RecoveryResetRequest request, PasswordRecoveryService recovery, HttpContext context, SignInManager<AppUser> signIn) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 || request.Code is null || request.Code.Length > 32 || request.NewPassword is null || request.NewPassword.Length > 1024)
                return Results.BadRequest("Enter your account email, recovery code, and new password.");
            var error = await recovery.ResetAsync(request.Email, request.Code, request.NewPassword, context.RequestAborted);
            if (error is not null) return Results.BadRequest(error);
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireRateLimiting("recovery-reset");
        account.MapPost("/setup", SetupAsync).RequireRateLimiting("account");
        account.MapPost("/login", async (LoginRequest request, SignInManager<AppUser> signIn, UserManager<AppUser> users, InventoryDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || request.Password is null || request.Password.Length > 1024)
                return Results.BadRequest("Enter your email and password.");
            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is null || !await db.WorkspaceMembers.AnyAsync(x => x.UserId == user.Id && x.WorkspaceId == DbSeeder.WorkspaceId))
                return Results.Text("Email or password is incorrect, or sign-in is temporarily locked.", statusCode: 401);
            var result = await signIn.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
            return result.Succeeded ? Results.NoContent() : Results.Text("Email or password is incorrect, or sign-in is temporarily locked.", statusCode: 401);
        }).RequireRateLimiting("account");
        account.MapPost("/logout", async (SignInManager<AppUser> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
        account.MapPost("/password", async (ChangePasswordRequest request, HttpContext context, UserManager<AppUser> users, SignInManager<AppUser> signIn) =>
        {
            var user = await users.GetUserAsync(context.User);
            if (user is null) return Results.Unauthorized();
            if (request.NewPassword is null || request.CurrentPassword is null || request.NewPassword.Length > 1024 || request.CurrentPassword.Length > 1024) return Results.BadRequest("Password is too long.");
            var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded) return Results.BadRequest(string.Join(" ", result.Errors.Select(x => x.Description)));
            await signIn.RefreshSignInAsync(user);
            return Results.NoContent();
        }).RequireAuthorization().RequireRateLimiting("account");
    }

    private static async Task<IResult> SetupAsync(SetupRequest request, HttpContext context, InventoryDbContext db, IConfiguration configuration, UserManager<AppUser> users, SignInManager<AppUser> signIn)
    {
        var expected = configuration["AccountSetup:Code"];
        if (expected is null || expected.Length < 32 || request.SetupCode is null || request.SetupCode.Length > 1024 ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)), SHA256.HashData(Encoding.UTF8.GetBytes(request.SetupCode.Trim()))))
            return Results.BadRequest("The setup code is invalid. Use the code generated on the server PC.");
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 || request.Password is null || string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 100 || request.Password.Length > 1024)
            return Results.BadRequest("Enter a display name (up to 100 characters) and a password.");
        await SetupGate.WaitAsync(context.RequestAborted);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(context.RequestAborted);
            // Serialize setup across processes as well as concurrent requests.
            if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(734928165)", context.RequestAborted);
            if (await db.Users.AnyAsync(x => x.PasswordHash != null)) return Results.Conflict("Account setup is already complete. Sign in instead.");
            var user = await users.FindByIdAsync(DbSeeder.GregUserId.ToString());
            if (user is null || !await db.WorkspaceMembers.AnyAsync(x => x.UserId == user.Id && x.WorkspaceId == DbSeeder.WorkspaceId && x.Role == "Owner"))
                return Results.Conflict("The existing household owner could not be found. No inventory has been changed.");
            user.Email = request.Email.Trim(); user.UserName = request.Email.Trim(); user.DisplayName = request.DisplayName.Trim();
            user.LockoutEnabled = true;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            var result = await users.AddPasswordAsync(user, request.Password);
            if (!result.Succeeded) return Results.BadRequest(string.Join(" ", result.Errors.Select(x => x.Description)));
            await transaction.CommitAsync(context.RequestAborted);
            await signIn.SignInAsync(user, isPersistent: true);
            return Results.NoContent();
        }
        finally { SetupGate.Release(); }
    }
}

public sealed record LoginRequest(string Email, string Password, bool RememberMe);
public sealed record SetupRequest(string Email, string DisplayName, string Password, string SetupCode);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record RecoveryCodeRequest(string Email);
public sealed record RecoveryResetRequest(string Email, string Code, string NewPassword);
