using System.Net.Http.Json;
using System.Text.Json;

namespace Ggd.AuthTests;

internal static class ItemDetailsChecks
{
    public static async Task RunAsync(HttpClient client, Guid workspaceId, Guid locationId)
    {
        var created = await client.PostAsJsonAsync("/api/items", new {
            workspaceId, locationId, name = "Green drill", quantity = 2,
            category = " Tools ", tags = new[] { "Tools", "tools", " cordless ", "" },
            brand = " Greenworks ", model = "Example model", serialNumber = "TEST-SERIAL-123",
            purchaseDate = "2026-01-15", purchasePrice = 0m, replacementValue = 99.95m,
            warrantyExpiresOn = "2027-01-15", warrantyNotes = "Limited coverage", condition = "Good"
        });
        var item = await Read(created, 201); var id = item.GetProperty("id").GetGuid();
        Check(item.GetProperty("category").GetString() == "Tools", "trim category");
        Check(item.GetProperty("tags").EnumerateArray().Select(x => x.GetString()).SequenceEqual(new[] { "Tools", "cordless" }), "normalize and deduplicate tags");
        Check(item.GetProperty("purchaseDate").GetString() == "2026-01-15", "date-only round trip");
        Check(item.GetProperty("purchasePrice").GetDecimal() == 0m && item.GetProperty("replacementValue").GetDecimal() == 99.95m, "zero and fractional prices");
        var listed = await client.GetFromJsonAsync<JsonElement>($"/api/items?workspaceId={workspaceId}");
        Check(listed.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id).GetProperty("serialNumber").GetString() == "TEST-SERIAL-123", "list API returns details");

        item = await Read(await client.PatchAsJsonAsync($"/api/items/{id}", new { workspaceId, name = "Green drill", quantity = 2 }), 200);
        Check(item.GetProperty("brand").GetString() == "Greenworks" && item.GetProperty("purchasePrice").GetDecimal() == 0m, "legacy update preserves omitted details");
        item = await Read(await client.PatchAsJsonAsync($"/api/items/{id}", new { workspaceId, name = "Green drill", quantity = 2, brand = "Ryobi" }), 200);
        Check(item.GetProperty("brand").GetString() == "Ryobi" && item.GetProperty("category").GetString() == "Tools", "partial metadata update");
        var activityUrl = $"/api/activity?workspaceId={workspaceId}&subjectType=item&subjectId={id}";
        var activity = await client.GetFromJsonAsync<JsonElement>(activityUrl);
        Check(activity.GetProperty("entries").EnumerateArray().Any(x => x.GetProperty("details").GetString()!.Contains("Brand: Greenworks → Ryobi")), "readable metadata audit");
        var beforeCount = activity.GetProperty("entries").GetArrayLength();
        await Read(await client.PatchAsJsonAsync($"/api/items/{id}", new { workspaceId, name = "Green drill", quantity = 2, tags = new[] { "Tools", "cordless" } }), 200);
        activity = await client.GetFromJsonAsync<JsonElement>(activityUrl);
        Check(activity.GetProperty("entries").GetArrayLength() == beforeCount, "equal tags do not create a spurious change");

        var locked = await Read(await client.PatchAsJsonAsync($"/api/items/{id}/edit-lock", new { workspaceId, isEditLocked = true }), 200);
        Check(locked.GetProperty("category").GetString() == "Tools", "lock response retains metadata");
        await Read(await client.PatchAsJsonAsync($"/api/items/{id}", new { workspaceId, name = "Green drill", quantity = 2, brand = "Blocked" }), 409);
        await Read(await client.PatchAsJsonAsync($"/api/items/{id}/edit-lock", new { workspaceId, isEditLocked = false }), 200);
        foreach (var bad in new[] { -1m, 1.001m, 10_000_000_000m })
            await Read(await client.PatchAsJsonAsync($"/api/items/{id}", new { workspaceId, name = "Green drill", quantity = 2, purchasePrice = bad }), 400);
        await Read(await client.PatchAsJsonAsync($"/api/items/{id}", new { workspaceId, name = "Green drill", quantity = 2, category = new string('x', 101) }), 400);
        await Read(await client.PatchAsJsonAsync($"/api/items/{id}", new { workspaceId, name = "Green drill", quantity = 2, tags = Enumerable.Repeat("tag", 21).ToArray() }), 400);
        var clear = new Dictionary<string, object?> {
            ["workspaceId"] = workspaceId, ["name"] = "Green drill", ["quantity"] = 2,
            ["category"] = null, ["tags"] = Array.Empty<string>(), ["brand"] = null, ["model"] = null, ["serialNumber"] = null,
            ["purchaseDate"] = null, ["purchasePrice"] = null, ["replacementValue"] = null,
            ["warrantyExpiresOn"] = null, ["warrantyNotes"] = null, ["condition"] = null
        };
        item = await Read(await client.PatchAsJsonAsync($"/api/items/{id}", clear), 200);
        Check(item.GetProperty("purchasePrice").ValueKind == JsonValueKind.Null && item.GetProperty("category").ValueKind == JsonValueKind.Null && item.GetProperty("tags").GetArrayLength() == 0, "explicit clearing");
        var legacy = await Read(await client.PostAsJsonAsync("/api/items", new { workspaceId, locationId, name = "Simple item", quantity = 1 }), 201);
        Check(legacy.GetProperty("category").ValueKind == JsonValueKind.Null && legacy.GetProperty("tags").GetArrayLength() == 0, "all details optional");
        Console.WriteLine("PASS: optional metadata, normalization, dates/money, list and mutation responses, partial/legacy updates, locks, validation, clearing, and activity history.");
    }
    private static void Check(bool condition, string label) { if (!condition) throw new Exception("FAIL: " + label); }
    private static async Task<JsonElement> Read(HttpResponseMessage response, int expected) {
        var body = await response.Content.ReadAsStringAsync();
        Check((int)response.StatusCode == expected, $"Expected {expected}, got {(int)response.StatusCode}: {body}");
        return response.IsSuccessStatusCode ? JsonSerializer.Deserialize<JsonElement>(body) : default;
    }
}
