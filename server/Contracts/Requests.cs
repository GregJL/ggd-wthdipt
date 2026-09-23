using Ggd.Api.Models;

namespace Ggd.Api.Contracts;

public sealed record CreateLocationRequest(Guid WorkspaceId, Guid? ParentId, string Name, LocationKind Type, LocationStatus Status, string? Note);
public sealed record CreateItemRequest(Guid WorkspaceId, Guid LocationId, string Name, int Quantity, string? Note);
public sealed record MoveItemRequest(Guid WorkspaceId, Guid DestinationLocationId);
public sealed record MoveLocationRequest(Guid WorkspaceId, Guid DestinationParentId);
public sealed record SetMoveLockRequest(Guid WorkspaceId, bool IsMoveLocked);
public sealed record SetEditLockRequest(Guid WorkspaceId, bool IsEditLocked);
public sealed record UpdateLocationRequest(Guid WorkspaceId, string Name, LocationKind Type, LocationStatus Status, string? Note);
public sealed record UpdateItemRequest(Guid WorkspaceId, string Name, int Quantity, string? Note);
public sealed record LocationLifecycleRequest(Guid WorkspaceId);
public sealed record UpdatePhotoRequest(Guid WorkspaceId, string? Caption);
