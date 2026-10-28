using System.Security.Cryptography;
using System.Text;
using Ggd.Api.Data;
using Ggd.Api.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ggd.Api.Security;

public sealed class PasswordRecoveryService(InventoryDbContext db, UserManager<AppUser> users, IRecoveryEmail mail,
    IDataProtectionProvider protection, TimeProvider clock, ILogger<PasswordRecoveryService> logger)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly IDataProtector protector = protection.CreateProtector("GGD.PasswordRecovery.CodeHash.v1");
    public const string RequestMessage = "If that account can be recovered, a code will be sent to its email address. Check your inbox and spam folder.";
    public const string InvalidCode = "The code is invalid or expired. Request a new code and try again.";

    public async Task RequestAsync(string email, CancellationToken cancellationToken)
    {
        string? recipient = null; string? code = null;
        await Gate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await LockAsync(cancellationToken);
            var user = await users.FindByEmailAsync(email.Trim());
            if (user?.PasswordHash is null || string.IsNullOrWhiteSpace(user.Email) ||
                !await db.WorkspaceMembers.AnyAsync(x => x.UserId == user.Id && x.WorkspaceId == DbSeeder.WorkspaceId, cancellationToken)) return;
            var now = clock.GetUtcNow();
            var challenge = await db.PasswordRecoveries.FindAsync([user.Id], cancellationToken);
            if (challenge is not null && now < challenge.CreatedAt.AddMinutes(1)) return;
            code = RandomNumberGenerator.GetInt32(100_000_000).ToString("D8");
            if (challenge is null) { challenge = new PasswordRecovery { UserId = user.Id }; db.PasswordRecoveries.Add(challenge); }
            // The database contains only a protected hash; neither plaintext codes nor reusable reset tokens.
            challenge.ProtectedCodeHash = protector.Protect(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))));
            challenge.SecurityStamp = await users.GetSecurityStampAsync(user);
            challenge.CreatedAt = now; challenge.ExpiresAt = now.AddMinutes(10); challenge.Attempts = 0;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            recipient = user.Email; // Always the persisted address, never a separate delivery address from the request.
        }
        finally { Gate.Release(); }
        if (recipient is not null && code is not null)
        {
            try { await mail.SendAsync(recipient, code, cancellationToken); }
            catch (Exception) { logger.LogWarning("Password recovery email delivery failed. Check the Gmail configuration on the server PC."); }
        }
    }

    public async Task<string?> ResetAsync(string email, string code, string password, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await LockAsync(cancellationToken);
            var user = await users.FindByEmailAsync(email.Trim());
            if (user?.PasswordHash is null || !await db.WorkspaceMembers.AnyAsync(x => x.UserId == user.Id && x.WorkspaceId == DbSeeder.WorkspaceId, cancellationToken)) return InvalidCode;
            var challenge = await db.PasswordRecoveries.FindAsync([user.Id], cancellationToken);
            if (challenge is null || challenge.ExpiresAt <= clock.GetUtcNow() || challenge.Attempts >= 5 ||
                challenge.SecurityStamp != await users.GetSecurityStampAsync(user)) return InvalidCode;
            bool valid;
            try
            {
                var expected = Convert.FromHexString(protector.Unprotect(challenge.ProtectedCodeHash));
                var actual = SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim()));
                valid = CryptographicOperations.FixedTimeEquals(expected, actual);
            }
            catch (CryptographicException) { valid = false; }
            if (!valid)
            {
                challenge.Attempts++;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return InvalidCode;
            }
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var result = await users.ResetPasswordAsync(user, token, password);
            if (!result.Succeeded) return string.Join(" ", result.Errors.Select(x => x.Code.StartsWith("Password", StringComparison.Ordinal) ? x.Description : InvalidCode).Distinct());
            user.AccessFailedCount = 0; user.LockoutEnd = null;
            if (!(await users.UpdateAsync(user)).Succeeded) return InvalidCode;
            db.PasswordRecoveries.Remove(challenge);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        finally { Gate.Release(); }
    }

    private async Task LockAsync(CancellationToken cancellationToken)
    {
        if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(734928166)", cancellationToken);
    }
}
