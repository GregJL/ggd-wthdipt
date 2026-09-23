namespace Ggd.Api.Models;

public sealed class AppUser {
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public string DisplayName { get; set; } = "";
    public ICollection<WorkspaceMember> Memberships { get; set; } = [];
}

public sealed class Workspace {
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public ICollection<WorkspaceMember> Members { get; set; } = [];
    public ICollection<Location> Locations { get; set; } = [];
}

public sealed class WorkspaceMember {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Role { get; set; } = "Member";
    public AppUser User { get; set; } = null!;
    public Workspace Workspace { get; set; } = null!;
}

public enum LocationStatus { Active, Planned, Temporary, Inactive }
public enum LocationKind { Floor, Room, Area, Storage }

public sealed class Location {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Guid? ParentId { get; set; }
    public required string Name { get; set; }
    public LocationKind Kind { get; set; }
    public LocationStatus Status { get; set; } = LocationStatus.Active;
    public bool IsMoveLocked { get; set; }
    public bool IsEditLocked { get; set; }
    public string? Note { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public Location? Parent { get; set; }
    public ICollection<Location> Children { get; set; } = [];
    public ICollection<InventoryItem> Items { get; set; } = [];
    public ICollection<LocationMovement> Movements { get; set; } = [];
    public ICollection<InventoryPhoto> Photos { get; set; } = [];
}

public sealed class InventoryItem {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Guid LocationId { get; set; }
    public required string Name { get; set; }
    public int Quantity { get; set; } = 1;
    public bool IsMoveLocked { get; set; }
    public bool IsEditLocked { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Location Location { get; set; } = null!;
    public ICollection<ItemMovement> Movements { get; set; } = [];
    public ICollection<InventoryPhoto> Photos { get; set; } = [];
}

public sealed class InventoryPhoto {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? LocationId { get; set; }
    public required string StoredFileName { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public string? Caption { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public InventoryItem? Item { get; set; }
    public Location? Location { get; set; }
}

public sealed class ItemMovement {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Guid ItemId { get; set; }
    public Guid FromLocationId { get; set; }
    public Guid ToLocationId { get; set; }
    public DateTimeOffset MovedAt { get; set; } = DateTimeOffset.UtcNow;
    public InventoryItem Item { get; set; } = null!;
}

public sealed class LocationMovement {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Guid LocationId { get; set; }
    public Guid FromParentId { get; set; }
    public Guid ToParentId { get; set; }
    public DateTimeOffset MovedAt { get; set; } = DateTimeOffset.UtcNow;
    public Location Location { get; set; } = null!;
}
