using Ggd.Api.Models;

namespace Ggd.Api.Contracts;

public sealed record LocationResponse(
    Guid Id,
    Guid WorkspaceId,
    Guid? ParentId,
    string Name,
    LocationKind Type,
    LocationStatus Status,
    bool IsMoveLocked,
    bool IsEditLocked,
    string? Note);

public sealed record ItemResponse(
    Guid Id,
    Guid WorkspaceId,
    Guid LocationId,
    string Name,
    int Quantity,
    bool IsMoveLocked,
    bool IsEditLocked,
    string? Note,
    DateTimeOffset CreatedAt);

public sealed record LocationDeletionImpactResponse(
    Guid LocationId,
    bool IsRoot,
    int ChildLocationCount,
    int ItemCount,
    int PhotoCount,
    int MovementReferenceCount,
    bool CanDelete);

public sealed record MovementHistoryResponse(
    Guid Id,
    Guid FromLocationId,
    string FromLocationName,
    Guid ToLocationId,
    string ToLocationName,
    DateTimeOffset MovedAt);

public sealed record PhotoResponse(
    Guid Id,
    Guid WorkspaceId,
    Guid? ItemId,
    Guid? LocationId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string? Caption,
    DateTimeOffset CreatedAt,
    string ContentUrl);
