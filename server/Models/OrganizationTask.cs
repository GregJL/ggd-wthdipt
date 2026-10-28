namespace Ggd.Api.Models;

public sealed class OrganizationTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string Title { get; set; } = "";
    public string? Note { get; set; }
    // References are optional snapshots; deleting inventory must not erase the task.
    public Guid? ItemId { get; set; }
    public Guid? LocationId { get; set; }
    public string? ReferenceName { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
