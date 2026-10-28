namespace Ggd.Api.Models;

// Deliberately has no foreign keys to inventory entities: history survives deletion.
public sealed class ActivityEntry
{
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public long Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string SubjectType { get; set; } = "";
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = "";
    public string Action { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Details { get; set; } = "";
    public string LocationPath { get; set; } = "";
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}
