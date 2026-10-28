using Microsoft.AspNetCore.Identity;

namespace Ggd.Api.Models;

public sealed class AppUser : IdentityUser<Guid> {
    public AppUser() { Id = Guid.NewGuid(); }
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
    public bool IsStaging { get; set; }
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
    public bool NeedsSorting { get; set; }
    public Guid? PlannedLocationId { get; set; }
    public string? Category { get; set; }
    public string[] Tags { get; set; } = [];
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? ReplacementValue { get; set; }
    public DateOnly? WarrantyExpiresOn { get; set; }
    public string? WarrantyNotes { get; set; }
    public string? Condition { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Location Location { get; set; } = null!;
    public ICollection<ItemMovement> Movements { get; set; } = [];
    public ICollection<InventoryPhoto> Photos { get; set; } = [];
}

public enum PhotoStage { General, Before, After }

public sealed class InventoryPhoto {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? LocationId { get; set; }
    public required string StoredFileName { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public PhotoStage Stage { get; set; }
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
