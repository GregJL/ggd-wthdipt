namespace Ggd.Api.Models;

public sealed class PasswordRecovery
{
    public Guid UserId { get; set; }
    public string ProtectedCodeHash { get; set; } = "";
    public string SecurityStamp { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int Attempts { get; set; }
}
