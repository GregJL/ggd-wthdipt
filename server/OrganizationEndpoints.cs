using Ggd.Api.Contracts;
using Ggd.Api.Data;
using Ggd.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ggd.Api;

public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this WebApplication app)
    {
        app.MapPatch("/api/items/{id:guid}/organization", async (Guid id, ItemOrganizationRequest request, InventoryDbContext db) =>
        {
            var item = await db.Items.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == request.WorkspaceId);
            if (item is null) return Results.NotFound();
            if (item.IsEditLocked) return Results.Conflict("Unlock editing before changing this item's organization plan.");
            if (request.PlannedLocationId is Guid planned)
            {
                if (planned == item.LocationId) return Results.BadRequest("This item is already there. Clear the plan or choose another destination.");
                if (!await Available(planned, request.WorkspaceId, db)) return Results.BadRequest("Choose an available destination in this household.");
            }
            item.NeedsSorting = request.NeedsSorting; item.PlannedLocationId = request.PlannedLocationId;
            await db.SaveChangesAsync();
            return Results.Ok(ItemResponse.From(item));
        });
        var tasks = app.MapGroup("/api/organization/tasks");
        tasks.MapGet("/", async (Guid workspaceId, InventoryDbContext db) =>
            (await db.OrganizationTasks.AsNoTracking().Where(x => x.WorkspaceId == workspaceId).ToListAsync()).OrderBy(x => x.IsCompleted).ThenByDescending(x => x.CreatedAt));
        tasks.MapPost("/", async (OrganizationTaskRequest request, InventoryDbContext db) =>
        {
            var error = Validate(request); if (error is not null) return Results.BadRequest(error);
            var reference = await Reference(request, null, db);
            if (!reference.Valid) return Results.BadRequest("Choose an item or location from this household.");
            var task = new OrganizationTask { WorkspaceId = request.WorkspaceId };
            Apply(task, request, reference.Name); db.OrganizationTasks.Add(task);
            await db.SaveChangesAsync(); return Results.Created($"/api/organization/tasks/{task.Id}", task);
        });
        tasks.MapPatch("/{id:guid}", async (Guid id, OrganizationTaskRequest request, InventoryDbContext db) =>
        {
            var task = await db.OrganizationTasks.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == request.WorkspaceId);
            if (task is null) return Results.NotFound();
            var error = Validate(request); if (error is not null) return Results.BadRequest(error);
            var reference = await Reference(request, task, db);
            if (!reference.Valid) return Results.BadRequest("Choose an item or location from this household.");
            Apply(task, request, reference.Name); await db.SaveChangesAsync(); return Results.Ok(task);
        });
        tasks.MapDelete("/{id:guid}", async (Guid id, Guid workspaceId, InventoryDbContext db) =>
        {
            var task = await db.OrganizationTasks.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == workspaceId);
            if (task is null) return Results.NotFound();
            db.OrganizationTasks.Remove(task); await db.SaveChangesAsync(); return Results.NoContent();
        });
    }
    private static string? Validate(OrganizationTaskRequest request) =>
        string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200 ? "Enter a task title of up to 200 characters." :
        request.Note?.Length > 2000 ? "Task notes can have up to 2000 characters." :
        request.ItemId.HasValue && request.LocationId.HasValue ? "Link either an item or a location, not both." : null;
    private static void Apply(OrganizationTask task, OrganizationTaskRequest request, string? name)
    {
        task.Title = request.Title.Trim(); task.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        task.ItemId = request.ItemId; task.LocationId = request.LocationId; task.ReferenceName = name;
        if (task.IsCompleted != request.IsCompleted) task.CompletedAt = request.IsCompleted ? DateTimeOffset.UtcNow : null;
        task.IsCompleted = request.IsCompleted;
    }
    private static async Task<(bool Valid, string? Name)> Reference(OrganizationTaskRequest request, OrganizationTask? previous, InventoryDbContext db)
    {
        if (request.ItemId is Guid itemId)
        {
            var name = await db.Items.Where(x => x.Id == itemId && x.WorkspaceId == request.WorkspaceId).Select(x => x.Name).FirstOrDefaultAsync();
            if (name is not null) return (true, name);
            return previous is not null && previous.ItemId == itemId ? (true, previous.ReferenceName) : (false, null);
        }
        if (request.LocationId is Guid locationId)
        {
            var name = await db.Locations.Where(x => x.Id == locationId && x.WorkspaceId == request.WorkspaceId).Select(x => x.Name).FirstOrDefaultAsync();
            if (name is not null) return (true, name);
            return previous is not null && previous.LocationId == locationId ? (true, previous.ReferenceName) : (false, null);
        }
        return (true, null);
    }
    private static async Task<bool> Available(Guid id, Guid workspaceId, InventoryDbContext db)
    {
        var seen = new HashSet<Guid>(); Guid? cursor = id;
        while (cursor is Guid current && seen.Add(current))
        {
            var location = await db.Locations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == current && x.WorkspaceId == workspaceId);
            if (location is null || location.Status == LocationStatus.Inactive) return false;
            cursor = location.ParentId;
        }
        return cursor is null;
    }
}
