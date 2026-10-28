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
            .Where(e => e.Entity is Location or InventoryItem or InventoryPhoto or OrganizationTask)
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

            if (entry.Entity is OrganizationTask task)
            {
                activity.SubjectType = task.ItemId.HasValue ? "item" : task.LocationId.HasValue ? "location" : "task";
                activity.SubjectId = task.ItemId ?? task.LocationId ?? task.Id;
                activity.SubjectName = task.ReferenceName ?? task.Title;
                Guid? locationId = task.LocationId;
                if (task.ItemId is Guid linkedItem)
                    locationId = await db.Items.Where(x => x.Id == linkedItem && x.WorkspaceId == task.WorkspaceId).Select(x => (Guid?)x.LocationId).FirstOrDefaultAsync(cancellationToken);
                activity.LocationPath = Path(locationId);
                var taskVerb = !added && !deleted && changes.Any(p => p.Metadata.Name == "IsCompleted") ? (task.IsCompleted ? "Completed" : "Reopened") : verb;
                activity.Action = "task-" + taskVerb.ToLowerInvariant();
                activity.Summary = $"{taskVerb} organization task “{task.Title}”";
                activity.Details = added || deleted ? task.Note ?? "Organization task" : string.Join("\n", changes
                    .Where(p => p.Metadata.Name is not ("ItemId" or "LocationId" or "ReferenceName" or "CompletedAt"))
                    .Select(Describe));
                if (task.ReferenceName is not null) activity.Details += $"\nRelated: {task.ReferenceName}";
            }
            else if (entry.Entity is InventoryPhoto photo)
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
                activity.Details = $"File: {photo.OriginalFileName}\nPhoto label: {photo.Stage}";
                if (added || deleted)
                {
                    if (!string.IsNullOrEmpty(photo.Caption)) activity.Details += $"\nCaption: {photo.Caption}";
                }
                else activity.Details += "\n" + string.Join("\n", changes.Where(p => p.Metadata.Name is "Caption" or "Stage").Select(Describe));
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
                        ? $"Location: {Path((Guid?)p.OriginalValue)} → {Path((Guid?)p.CurrentValue)}"
                        : p.Metadata.Name == "PlannedLocationId" ? $"Planned destination: {(p.OriginalValue is Guid oldPlan ? Path(oldPlan) : "(none)")} → {(p.CurrentValue is Guid newPlan ? Path(newPlan) : "(none)")}"
                        : Describe(p)));
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
            "NeedsSorting" => "Sorting status", "IsStaging" => "Staging area", "IsCompleted" => "Task status", "Stage" => "Photo label",
            "Kind" => "Type", "Note" => "Notes", "SerialNumber" => "Serial number",
            "PurchaseDate" => "Purchase date", "PurchasePrice" => "Purchase price (USD each)",
            "ReplacementValue" => "Replacement value (USD each)", "WarrantyExpiresOn" => "Warranty ends", "WarrantyNotes" => "Warranty notes", _ => property.Metadata.Name
        };
        string Value(object? value) => value switch { null => "(empty)", "" => "(empty)", bool b => property.Metadata.Name switch { "NeedsSorting" => b ? "Needs sorting" : "Sorted", "IsStaging" => b ? "Staging area" : "Regular location", "IsCompleted" => b ? "Done" : "Open", _ => b ? "Locked" : "Unlocked" }, IEnumerable<string> tags => string.Join(", ", tags), DateOnly date => date.ToString("yyyy-MM-dd"), decimal amount => amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), _ => value.ToString()! };
        return $"{label}: {Value(property.OriginalValue)} → {Value(property.CurrentValue)}";
    }
}
