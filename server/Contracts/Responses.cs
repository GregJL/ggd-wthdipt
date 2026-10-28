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
    DateTimeOffset CreatedAt)
{
    public string? Category { get; init; }
    public string[] Tags { get; init; } = [];
    public string? Brand { get; init; }
    public string? Model { get; init; }
    public string? SerialNumber { get; init; }
    public DateOnly? PurchaseDate { get; init; }
    public decimal? PurchasePrice { get; init; }
    public decimal? ReplacementValue { get; init; }
    public DateOnly? WarrantyExpiresOn { get; init; }
    public string? WarrantyNotes { get; init; }
    public string? Condition { get; init; }
    public static ItemResponse From(InventoryItem item) =>
        new(item.Id, item.WorkspaceId, item.LocationId, item.Name, item.Quantity, item.IsMoveLocked, item.IsEditLocked, item.Note, item.CreatedAt)
        {
            Category = item.Category,
            Tags = item.Tags,
            Brand = item.Brand,
            Model = item.Model,
            SerialNumber = item.SerialNumber,
            PurchaseDate = item.PurchaseDate,
            PurchasePrice = item.PurchasePrice,
            ReplacementValue = item.ReplacementValue,
            WarrantyExpiresOn = item.WarrantyExpiresOn,
            WarrantyNotes = item.WarrantyNotes,
            Condition = item.Condition,
        };
}

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
