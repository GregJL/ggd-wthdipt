using Ggd.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ggd.Api.Data;

public static class ActivityQueries
{
    public static async Task<IQueryable<ActivityEntry>> ForLocationAsync(InventoryDbContext db, Guid workspaceId, Guid locationId)
    {
        var hierarchy = await db.Locations.AsNoTracking().Where(x => x.WorkspaceId == workspaceId)
            .Select(x => new { x.Id, x.ParentId }).ToListAsync();
        var children = hierarchy.Where(x => x.ParentId.HasValue).ToLookup(x => x.ParentId!.Value, x => x.Id);
        var descendants = new HashSet<Guid>();
        var pending = new Queue<Guid>();
        pending.Enqueue(locationId);
        while (pending.TryDequeue(out var id))
        {
            if (!descendants.Add(id)) continue;
            foreach (var child in children[id]) pending.Enqueue(child);
        }
        var locationIds = descendants.ToArray();
        var itemIds = db.Items.Where(x => x.WorkspaceId == workspaceId && locationIds.Contains(x.LocationId)).Select(x => x.Id);
        return db.ActivityEntries.AsNoTracking().Where(x => x.WorkspaceId == workspaceId &&
            ((x.SubjectType == "location" && locationIds.Contains(x.SubjectId)) ||
             (x.SubjectType == "item" && itemIds.Contains(x.SubjectId))));
    }
}
