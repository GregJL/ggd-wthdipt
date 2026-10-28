using Ggd.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Ggd.Api.Data;

internal static class ActivityRecorder
{
    public static async Task<List<ActivityEntry>> CaptureAsync(InventoryDbContext db, CancellationToken cancellationToken)
    {
        db.ChangeTracker.DetectChanges();
        var entries = db.ChangeTracker.Entries()
            .Where(e => e.Entity is Location or InventoryItem or InventoryPhoto)
            .Where(e => e.State is EntityState.Added or EntityState.Deleted ||
                e.State == EntityState.Modified && e.Properties.Any(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)))
            .ToList();
        if (entries.Count == 0) return [];

        var workspaceIds = entries.Select(e => (Guid)e.Property("WorkspaceId").CurrentValue!).Distinct().ToArray();
        var locations = await db.Locations.AsNoTracking().Where(x => workspaceIds.Contains(x.WorkspaceId))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var entry in entries.Where(e => e.Entity is Location))
        {
            var location = (Location)entry.Entity;
            locations[location.Id] = location;
        }
        string Path(Guid? id)
        {
            if (id is null) return "Home";
            var names = new List<string>();
            var seen = new HashSet<Guid>();
            while (id is Guid current && seen.Add(current) && locations.TryGetValue(current, out var location))
            {
                names.Insert(0, location.Name);
                id = location.ParentId;
            }
            return names.Count == 0 ? "Unknown location" : string.Join(" › ", names);
        }

        var result = new List<ActivityEntry>();
        foreach (var entry in entries)
        {
            var activity = new ActivityEntry { WorkspaceId = (Guid)entry.Property("WorkspaceId").CurrentValue! };
            var changes = entry.Properties.Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)).ToList();
            var added = entry.State == EntityState.Added;
            var deleted = entry.State == EntityState.Deleted;
            var verb = added ? "Added" : deleted ? "Deleted" : "Updated";
            activity.Action = added ? "added" : deleted ? "deleted" : "updated";

            if (entry.Entity is InventoryPhoto photo)
            {
                activity.SubjectType = photo.ItemId.HasValue ? "item" : "location";
                activity.SubjectId = photo.ItemId ?? photo.LocationId!.Value;
                if (photo.ItemId is Guid itemId)
                {
                    var item = db.Items.Local.FirstOrDefault(x => x.Id == itemId && x.WorkspaceId == photo.WorkspaceId)
                        ?? await db.Items.AsNoTracking().FirstOrDefaultAsync(x => x.Id == itemId && x.WorkspaceId == photo.WorkspaceId, cancellationToken);
                    activity.SubjectName = item?.Name ?? "Deleted item";
                    activity.LocationPath = Path(item?.LocationId);
                }
                else
                {
                    activity.SubjectName = locations.GetValueOrDefault(activity.SubjectId)?.Name ?? "Deleted location";
                    activity.LocationPath = Path(photo.LocationId);
                }
                activity.Action = added ? "photo-added" : deleted ? "photo-deleted" : "photo-updated";
                activity.Summary = $"{verb} photo for {activity.SubjectType} “{activity.SubjectName}”";
                activity.Details = $"File: {photo.OriginalFileName}";
                if (added || deleted)
                {
                    if (!string.IsNullOrEmpty(photo.Caption)) activity.Details += $"\nCaption: {photo.Caption}";
                }
                else activity.Details += "\n" + string.Join("\n", changes.Where(p => p.Metadata.Name == "Caption").Select(Describe));
            }
            else
            {
                var isItem = entry.Entity is InventoryItem;
                activity.SubjectType = isItem ? "item" : "location";
                activity.SubjectId = (Guid)entry.Property("Id").CurrentValue!;
                activity.SubjectName = (string)entry.Property("Name").CurrentValue!;
                activity.LocationPath = Path(isItem ? ((InventoryItem)entry.Entity).LocationId : ((Location)entry.Entity).Id);
                activity.Summary = $"{verb} {activity.SubjectType} “{activity.SubjectName}”";
                if (added || deleted)
                {
                    activity.Details = isItem ? $"Quantity: {((InventoryItem)entry.Entity).Quantity}" : $"Type: {((Location)entry.Entity).Kind}";
                    if (deleted && isItem) activity.Details += "\nAttached photos and movement records were also removed.";
                }
                else
                {
                    activity.Details = string.Join("\n", changes.Select(p => p.Metadata.Name is "LocationId" or "ParentId"
                        ? $"Location: {Path((Guid?)p.OriginalValue)} → {Path((Guid?)p.CurrentValue)}" : Describe(p)));
                    if (changes.Any(p => p.Metadata.Name is "LocationId" or "ParentId"))
                    {
                        activity.Action = "moved";
                        activity.Summary = $"Moved {activity.SubjectType} “{activity.SubjectName}”";
                    }
                    else if (changes.Count == 1 && changes[0].Metadata.Name is "IsEditLocked" or "IsMoveLocked")
                    {
                        var locked = (bool)changes[0].CurrentValue!;
                        var lockType = changes[0].Metadata.Name == "IsEditLocked" ? "editing" : "movement";
                        activity.Action = locked ? "locked" : "unlocked";
                        activity.Summary = $"{(locked ? "Locked" : "Unlocked")} {lockType} for {activity.SubjectType} “{activity.SubjectName}”";
                    }
                    else if (changes.Any(p => p.Metadata.Name == "Status" &&
                        ((LocationStatus)p.CurrentValue! == LocationStatus.Inactive || (LocationStatus)p.OriginalValue! == LocationStatus.Inactive)))
                    {
                        var archived = ((Location)entry.Entity).Status == LocationStatus.Inactive;
                        activity.Action = archived ? "archived" : "restored";
                        activity.Summary = $"{(archived ? "Archived" : "Restored")} location “{activity.SubjectName}”";
                    }
                }
            }
            result.Add(activity);
        }
        return result;
    }

    private static string Describe(PropertyEntry property)
    {
        var label = property.Metadata.Name switch
        {
            "IsEditLocked" => "Edit lock", "IsMoveLocked" => "Move lock",
            "Kind" => "Type", "Note" => "Notes", "SerialNumber" => "Serial number",
            "PurchaseDate" => "Purchase date", "PurchasePrice" => "Purchase price (USD each)",
            "ReplacementValue" => "Replacement value (USD each)", "WarrantyExpiresOn" => "Warranty ends", "WarrantyNotes" => "Warranty notes", _ => property.Metadata.Name
        };
        static string Value(object? value) => value switch { null => "(empty)", "" => "(empty)", bool b => b ? "Locked" : "Unlocked", IEnumerable<string> tags => string.Join(", ", tags), DateOnly date => date.ToString("yyyy-MM-dd"), decimal amount => amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), _ => value.ToString()! };
        return $"{label}: {Value(property.OriginalValue)} → {Value(property.CurrentValue)}";
    }
}
