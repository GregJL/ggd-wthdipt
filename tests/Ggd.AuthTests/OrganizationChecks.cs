using System.Net.Http.Json;
using System.Text.Json;
using Ggd.Api.Data;
using Ggd.Api.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Ggd.AuthTests;

internal static class OrganizationChecks
{
    public static async Task RunAsync(HttpClient client, Guid workspaceId, Guid parentId, IServiceProvider services)
    {
        var stage = await CreateLocation("Sorting area", true); var stageId = stage.GetProperty("id").GetGuid();
        Check(stage.GetProperty("isStaging").GetBoolean(), "staging designation");
        var oldUpdate = await Read(await client.PatchAsJsonAsync($"/api/locations/{stageId}", new { workspaceId, name = "Sorting area", type = "storage", status = "temporary" }), 200);
        Check(oldUpdate.GetProperty("isStaging").GetBoolean(), "legacy location update preserves staging");
        var child = await Read(await client.PostAsJsonAsync("/api/locations", new { workspaceId, parentId = stageId, name = "Staging box", type = "storage", status = "active" }), 201);
        var childId = child.GetProperty("id").GetGuid();
        var destination = await CreateLocation("Permanent home", false); var destinationId = destination.GetProperty("id").GetGuid();
        var archived = await CreateLocation("Archived destination", false); var archivedId = archived.GetProperty("id").GetGuid();
        await Read(await client.PatchAsJsonAsync($"/api/locations/{archivedId}/archive", new { workspaceId }), 200);
        var item = await Read(await client.PostAsJsonAsync("/api/items", new { workspaceId, locationId = childId, name = "Organization test drill", quantity = 1 }), 201); var itemId = item.GetProperty("id").GetGuid();
        Guid foreignLocation; Guid foreignItem;
        using (var scope = services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var household = new Workspace { Name = "Other test household" };
            var location = new Location { WorkspaceId = household.Id, Name = "Other household location" };
            var other = new InventoryItem { WorkspaceId = household.Id, LocationId = location.Id, Name = "Other household item" };
            db.AddRange(household, location, other); await db.SaveChangesAsync(); foreignLocation = location.Id; foreignItem = other.Id;
        }
        foreach (var invalid in new[] { childId, foreignLocation, archivedId })
            await Read(await client.PatchAsJsonAsync($"/api/items/{itemId}/organization", new { workspaceId, needsSorting = true, plannedLocationId = invalid }), 400);
        await Read(await client.PatchAsJsonAsync($"/api/items/{itemId}/move-lock", new { workspaceId, isMoveLocked = true }), 200);
        item = await Read(await client.PatchAsJsonAsync($"/api/items/{itemId}/organization", new { workspaceId, needsSorting = true, plannedLocationId = destinationId }), 200);
        Check(item.GetProperty("locationId").GetGuid() == childId && item.GetProperty("plannedLocationId").GetGuid() == destinationId, "planning does not move items and ignores move lock");
        var impact = await Read(await client.GetAsync($"/api/locations/{destinationId}/deletion-impact?workspaceId={workspaceId}"), 200);
        Check(!impact.GetProperty("canDelete").GetBoolean() && impact.GetProperty("plannedItemCount").GetInt32() == 1, "planned destination deletion blocked");
        await Read(await client.DeleteAsync($"/api/locations/{destinationId}?workspaceId={workspaceId}"), 409);
        await Read(await client.PatchAsJsonAsync($"/api/items/{itemId}/move", new { workspaceId, destinationLocationId = destinationId }), 409);
        await Read(await client.PatchAsJsonAsync($"/api/items/{itemId}/move-lock", new { workspaceId, isMoveLocked = false }), 200);
        await Read(await client.PatchAsJsonAsync($"/api/items/{itemId}/edit-lock", new { workspaceId, isEditLocked = true }), 200);
        await Read(await client.PatchAsJsonAsync($"/api/items/{itemId}/organization", new { workspaceId, needsSorting = false, plannedLocationId = (Guid?)null }), 409);
        item = await Read(await client.PatchAsJsonAsync($"/api/items/{itemId}/move", new { workspaceId, destinationLocationId = destinationId }), 200);
        Check(item.GetProperty("id").GetGuid() == itemId && item.GetProperty("needsSorting").GetBoolean() && item.GetProperty("plannedLocationId").GetGuid() == destinationId, "move preserves identity and does not silently change organization metadata");
        await Read(await client.PatchAsJsonAsync($"/api/items/{itemId}/edit-lock", new { workspaceId, isEditLocked = false }), 200);
        item = await Read(await client.PatchAsJsonAsync($"/api/items/{itemId}/organization", new { workspaceId, needsSorting = false, plannedLocationId = (Guid?)null }), 200);
        Check(!item.GetProperty("needsSorting").GetBoolean() && item.GetProperty("plannedLocationId").ValueKind == JsonValueKind.Null, "mark sorted clears plan");

        await Read(await client.PostAsJsonAsync("/api/organization/tasks", new { workspaceId, title = "Denied", itemId = foreignItem }), 400);
        await Read(await client.PostAsJsonAsync("/api/organization/tasks", new { workspaceId, title = "Denied", itemId, locationId = childId }), 400);
        var task = await Read(await client.PostAsJsonAsync("/api/organization/tasks", new { workspaceId, title = "Sort test drill", note = "A real organization task", itemId }), 201); var taskId = task.GetProperty("id").GetGuid();
        Check(task.GetProperty("referenceName").GetString() == "Organization test drill", "linked task name snapshot");
        task = await UpdateTask(true); var completedAt = task.GetProperty("completedAt").GetString();
        Check(completedAt is not null, "task completion timestamp");
        Check((await UpdateTask(true)).GetProperty("completedAt").GetString() == completedAt, "task edits preserve completion timestamp");
        Check((await UpdateTask(false)).GetProperty("completedAt").ValueKind == JsonValueKind.Null, "task reopening");
        var list = await Read(await client.GetAsync($"/api/organization/tasks?workspaceId={workspaceId}"), 200);
        Check(list.EnumerateArray().Any(x => x.GetProperty("id").GetGuid() == taskId), "task list");

        using (var form = new MultipartFormDataContent()) {
            form.Add(new StringContent(workspaceId.ToString()), "workspaceId"); form.Add(new StringContent(itemId.ToString()), "itemId"); form.Add(new StringContent("before"), "stage");
            var file = new ByteArrayContent([0xff, 0xd8, 0xff, 0xd9]); file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg"); form.Add(file, "file", "before.jpg");
            var photo = await Read(await client.PostAsync("/api/photos", form), 201); var photoId = photo.GetProperty("id").GetGuid();
            Check(photo.GetProperty("stage").GetString() == "before", "before photo upload");
            photo = await Read(await client.PatchAsJsonAsync($"/api/photos/{photoId}", new { workspaceId, caption = "After sorting", stage = "after" }), 200);
            Check(photo.GetProperty("stage").GetString() == "after", "after photo relabel");
            photo = await Read(await client.PatchAsJsonAsync($"/api/photos/{photoId}", new { workspaceId, caption = "Legacy caption update" }), 200);
            Check(photo.GetProperty("stage").GetString() == "after", "legacy caption update retains photo stage");
            await Read(await client.PatchAsJsonAsync($"/api/photos/{photoId}", new { workspaceId, caption = "Denied", stage = 999 }), 400);
        }
        var history = await Read(await client.GetAsync($"/api/activity?workspaceId={workspaceId}&subjectType=item&subjectId={itemId}"), 200);
        Check(history.GetProperty("entries").EnumerateArray().Any(x => x.GetProperty("details").GetString()!.Contains("Planned destination")), "readable planning audit");
        await Read(await client.DeleteAsync($"/api/items/{itemId}?workspaceId={workspaceId}"), 204);
        task = await UpdateTask(true); Check(task.GetProperty("referenceName").GetString() == "Organization test drill", "linked task survives inventory deletion");
        history = await Read(await client.GetAsync($"/api/activity?workspaceId={workspaceId}&subjectType=item&subjectId={itemId}"), 200);
        Check(history.GetProperty("entries").EnumerateArray().Any(x => x.GetProperty("action").GetString() == "task-completed" && x.GetProperty("actorName").GetString() == "Greg"), "task audit attribution");
        await Read(await client.DeleteAsync($"/api/organization/tasks/{taskId}?workspaceId={workspaceId}"), 204);
        Console.WriteLine("PASS: staging, organization plans, independent locks, destination safety, tasks/reopening/deletion, retained references, photo labels, and audit attribution.");

        async Task<JsonElement> CreateLocation(string name, bool isStaging) => await Read(await client.PostAsJsonAsync("/api/locations", new { workspaceId, parentId, name, type = "storage", status = "temporary", isStaging }), 201);
        async Task<JsonElement> UpdateTask(bool isCompleted) => await Read(await client.PatchAsJsonAsync($"/api/organization/tasks/{taskId}", new { workspaceId, title = "Sort test drill", itemId, isCompleted }), 200);
    }
    private static void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); }
    private static async Task<JsonElement> Read(HttpResponseMessage response, int expected) {
        var body = await response.Content.ReadAsStringAsync(); Check((int)response.StatusCode == expected, $"Expected {expected}, got {(int)response.StatusCode}: {body}");
        return response.IsSuccessStatusCode && body.Length > 0 ? JsonSerializer.Deserialize<JsonElement>(body) : default;
    }
}
