using Ggd.Api.Contracts;
using Ggd.Api.Data;
using Ggd.Api.Models;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
// This is a private, single-machine home deployment. Keep using the existing
// user-secret connection string when the published app runs under Greg's account.
//builder.Configuration.AddUserSecrets("ggd-wthdipt-development", optional: true);
builder.Configuration.AddUserSecrets(
    System.Reflection.Assembly.GetExecutingAssembly(),
    optional: true);
builder.Services.AddDbContext<InventoryDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("Inventory")));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed(IsDevelopmentClientOrigin)));

var app = builder.Build();
app.UseCors();
app.UseDefaultFiles();
var staticContentTypes = new FileExtensionContentTypeProvider();
staticContentTypes.Mappings[".webmanifest"] = "application/manifest+json";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = staticContentTypes });
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

if (app.Environment.IsDevelopment())
    await DbSeeder.SeedAsync(app.Services);

var locations = app.MapGroup("/api/locations");
locations.MapGet("/", async (Guid workspaceId, Guid? parentId, InventoryDbContext db) =>
    await db.Locations.AsNoTracking()
        .Where(x => x.WorkspaceId == workspaceId && x.ParentId == parentId)
        .OrderBy(x => x.Name)
        .Select(x => new LocationResponse(x.Id, x.WorkspaceId, x.ParentId, x.Name, x.Kind, x.Status, x.IsMoveLocked, x.IsEditLocked, x.Note))
        .ToListAsync());
locations.MapGet("/all", async (Guid workspaceId, InventoryDbContext db) =>
    await db.Locations.AsNoTracking()
        .Where(x => x.WorkspaceId == workspaceId)
        .OrderBy(x => x.Name)
        .Select(x => new LocationResponse(x.Id, x.WorkspaceId, x.ParentId, x.Name, x.Kind, x.Status, x.IsMoveLocked, x.IsEditLocked, x.Note))
        .ToListAsync());
locations.MapPost("/", async (CreateLocationRequest request, InventoryDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("Name is required.");
    if (request.ParentId is Guid parentId && !await IsLocationAvailable(parentId, request.WorkspaceId, db))
        return Results.BadRequest("Parent location is unavailable or archived.");
    var location = new Location { WorkspaceId = request.WorkspaceId, ParentId = request.ParentId, Name = request.Name.Trim(), Kind = request.Type, Status = request.Status, IsMoveLocked = request.Type is LocationKind.Floor or LocationKind.Room, Note = request.Note?.Trim() };
    db.Locations.Add(location); await db.SaveChangesAsync();
    return Results.Created($"/api/locations/{location.Id}", new LocationResponse(location.Id, location.WorkspaceId, location.ParentId, location.Name, location.Kind, location.Status, location.IsMoveLocked, location.IsEditLocked, location.Note));
});
locations.MapPatch("/{id:guid}", async (Guid id, UpdateLocationRequest request, InventoryDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("Name is required.");
    var location = await db.Locations.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == request.WorkspaceId);
    if (location is null) return Results.NotFound();
    if (location.IsEditLocked) return Results.Conflict("Unlock editing for this location before changing it.");
    var name = request.Name.Trim();
    if (await db.Locations.AnyAsync(x => x.Id != id && x.WorkspaceId == request.WorkspaceId && x.ParentId == location.ParentId && x.Name == name))
        return Results.Conflict("Another location with that name already exists here.");

    location.Name = name;
    location.Kind = request.Type;
    location.Status = request.Status;
    location.Note = request.Note?.Trim();
    await db.SaveChangesAsync();
    return Results.Ok(new LocationResponse(location.Id, location.WorkspaceId, location.ParentId, location.Name, location.Kind, location.Status, location.IsMoveLocked, location.IsEditLocked, location.Note));
});
locations.MapPatch("/{id:guid}/edit-lock", async (Guid id, SetEditLockRequest request, InventoryDbContext db) =>
{
    var location = await db.Locations.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == request.WorkspaceId);
    if (location is null) return Results.NotFound();
    location.IsEditLocked = request.IsEditLocked;
    await db.SaveChangesAsync();
    return Results.Ok(new LocationResponse(location.Id, location.WorkspaceId, location.ParentId, location.Name, location.Kind, location.Status, location.IsMoveLocked, location.IsEditLocked, location.Note));
});
locations.MapGet("/{id:guid}/deletion-impact", async (Guid id, Guid workspaceId, InventoryDbContext db) =>
{
    if (!await db.Locations.AnyAsync(x => x.Id == id && x.WorkspaceId == workspaceId)) return Results.NotFound();
    return Results.Ok(await GetLocationDeletionImpact(id, workspaceId, db));
});
locations.MapPatch("/{id:guid}/archive", async (Guid id, LocationLifecycleRequest request, InventoryDbContext db) =>
{
    var location = await db.Locations.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == request.WorkspaceId);
    if (location is null) return Results.NotFound();
    if (location.ParentId is null) return Results.Conflict("Root locations cannot be archived.");
    location.Status = LocationStatus.Inactive;
    await db.SaveChangesAsync();
    return Results.Ok(new LocationResponse(location.Id, location.WorkspaceId, location.ParentId, location.Name, location.Kind, location.Status, location.IsMoveLocked, location.IsEditLocked, location.Note));
});
locations.MapPatch("/{id:guid}/restore", async (Guid id, LocationLifecycleRequest request, InventoryDbContext db) =>
{
    var location = await db.Locations.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == request.WorkspaceId);
    if (location is null) return Results.NotFound();
    if (location.ParentId is Guid parentId && !await IsLocationAvailable(parentId, request.WorkspaceId, db))
        return Results.Conflict("Restore the parent location first.");
    location.Status = LocationStatus.Active;
    await db.SaveChangesAsync();
    return Results.Ok(new LocationResponse(location.Id, location.WorkspaceId, location.ParentId, location.Name, location.Kind, location.Status, location.IsMoveLocked, location.IsEditLocked, location.Note));
});
locations.MapPatch("/{id:guid}/move-lock", async (Guid id, SetMoveLockRequest request, InventoryDbContext db) =>
{
    var location = await db.Locations.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == request.WorkspaceId);
    if (location is null) return Results.NotFound();
    location.IsMoveLocked = request.IsMoveLocked;
    await db.SaveChangesAsync();
    return Results.Ok(new LocationResponse(location.Id, location.WorkspaceId, location.ParentId, location.Name, location.Kind, location.Status, location.IsMoveLocked, location.IsEditLocked, location.Note));
});
locations.MapPatch("/{id:guid}/move", async (Guid id, MoveLocationRequest request, InventoryDbContext db) =>
{
    var location = await db.Locations.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == request.WorkspaceId);
    if (location is null) return Results.NotFound();
    if (location.ParentId is null) return Results.Conflict("Root locations cannot be moved.");
    if (location.IsMoveLocked) return Results.Conflict("Unlock this location before moving it.");
    if (location.Id == request.DestinationParentId) return Results.BadRequest("A location cannot contain itself.");
    if (location.ParentId == request.DestinationParentId) return Results.BadRequest("The location is already there.");

    var relationships = await db.Locations.AsNoTracking()
        .Where(x => x.WorkspaceId == request.WorkspaceId)
        .Select(x => new { x.Id, x.ParentId })
        .ToDictionaryAsync(x => x.Id, x => x.ParentId);
    if (!relationships.ContainsKey(request.DestinationParentId) || !await IsLocationAvailable(request.DestinationParentId, request.WorkspaceId, db))
        return Results.BadRequest("The destination is unavailable or archived.");

    Guid? cursor = request.DestinationParentId;
    while (cursor is Guid cursorId)
    {
        if (cursorId == location.Id) return Results.BadRequest("A location cannot be moved inside one of its descendants.");
        cursor = relationships.GetValueOrDefault(cursorId);
    }

    await using var transaction = await db.Database.BeginTransactionAsync();
    db.LocationMovements.Add(new LocationMovement
    {
        WorkspaceId = request.WorkspaceId,
        LocationId = location.Id,
        FromParentId = location.ParentId.Value,
        ToParentId = request.DestinationParentId
    });
    location.ParentId = request.DestinationParentId;
    await db.SaveChangesAsync();
    await transaction.CommitAsync();
    return Results.Ok(new LocationResponse(location.Id, location.WorkspaceId, location.ParentId, location.Name, location.Kind, location.Status, location.IsMoveLocked, location.IsEditLocked, location.Note));
});
locations.MapGet("/{id:guid}/history", async (Guid id, Guid workspaceId, InventoryDbContext db) =>
{
    if (!await db.Locations.AnyAsync(x => x.Id == id && x.WorkspaceId == workspaceId)) return Results.NotFound();
    return Results.Ok(await (
        from movement in db.LocationMovements.AsNoTracking()
        join fromLocation in db.Locations.AsNoTracking() on movement.FromParentId equals fromLocation.Id
        join toLocation in db.Locations.AsNoTracking() on movement.ToParentId equals toLocation.Id
        where movement.WorkspaceId == workspaceId && movement.LocationId == id
        orderby movement.MovedAt descending
        select new MovementHistoryResponse(movement.Id, fromLocation.Id, fromLocation.Name, toLocation.Id, toLocation.Name, movement.MovedAt)
    ).ToListAsync());
});

var items = app.MapGroup("/api/items");
items.MapGet("/", async (Guid workspaceId, Guid? locationId, string? query, InventoryDbContext db) =>
{
    var result = db.Items.AsNoTracking().Where(x => x.WorkspaceId == workspaceId);
    if (locationId is not null) result = result.Where(x => x.LocationId == locationId);
    if (!string.IsNullOrWhiteSpace(query)) result = result.Where(x => x.Name.Contains(query));
    return await result.OrderBy(x => x.Name)
        .Select(x => new ItemResponse(x.Id, x.WorkspaceId, x.LocationId, x.Name, x.Quantity, x.IsMoveLocked, x.IsEditLocked, x.Note, x.CreatedAt))
        .ToListAsync();
});
items.MapPost("/", async (CreateItemRequest request, InventoryDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || request.Quantity < 1) return Results.BadRequest("A name and positive quantity are required.");
    if (!await IsLocationAvailable(request.LocationId, request.WorkspaceId, db))
        return Results.BadRequest("Location is unavailable or archived.");
    var item = new InventoryItem { WorkspaceId = request.WorkspaceId, LocationId = request.LocationId, Name = request.Name.Trim(), Quantity = request.Quantity, Note = request.Note?.Trim() };
    db.Items.Add(item); await db.SaveChangesAsync();
    return Results.Created($"/api/items/{item.Id}", new ItemResponse(item.Id, item.WorkspaceId, item.LocationId, item.Name, item.Quantity, item.IsMoveLocked, item.IsEditLocked, item.Note, item.CreatedAt));
});
items.MapPatch("/{id:guid}", async (Guid id, UpdateItemRequest request, InventoryDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || request.Quantity < 1) return Results.BadRequest("A name and positive quantity are required.");
    var item = await db.Items.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == request.WorkspaceId);
    if (item is null) return Results.NotFound();
    if (item.IsEditLocked) return Results.Conflict("Unlock editing for this item before changing it.");

    item.Name = request.Name.Trim();
    item.Quantity = request.Quantity;
    item.Note = request.Note?.Trim();
    await db.SaveChangesAsync();
    return Results.Ok(new ItemResponse(item.Id, item.WorkspaceId, item.LocationId, item.Name, item.Quantity, item.IsMoveLocked, item.IsEditLocked, item.Note, item.CreatedAt));
});
items.MapPatch("/{id:guid}/edit-lock", async (Guid id, SetEditLockRequest request, InventoryDbContext db) =>
{
    var item = await db.Items.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == request.WorkspaceId);
    if (item is null) return Results.NotFound();
    item.IsEditLocked = request.IsEditLocked;
    await db.SaveChangesAsync();
    return Results.Ok(new ItemResponse(item.Id, item.WorkspaceId, item.LocationId, item.Name, item.Quantity, item.IsMoveLocked, item.IsEditLocked, item.Note, item.CreatedAt));
});
items.MapPatch("/{id:guid}/move-lock", async (Guid id, SetMoveLockRequest request, InventoryDbContext db) =>
{
    var item = await db.Items.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == request.WorkspaceId);
    if (item is null) return Results.NotFound();
    item.IsMoveLocked = request.IsMoveLocked;
    await db.SaveChangesAsync();
    return Results.Ok(new ItemResponse(item.Id, item.WorkspaceId, item.LocationId, item.Name, item.Quantity, item.IsMoveLocked, item.IsEditLocked, item.Note, item.CreatedAt));
});
items.MapPatch("/{id:guid}/move", async (Guid id, MoveItemRequest request, InventoryDbContext db) =>
{
    var item = await db.Items.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == request.WorkspaceId);
    if (item is null) return Results.NotFound();
    if (item.IsMoveLocked) return Results.Conflict("Unlock this item before moving it.");
    if (item.LocationId == request.DestinationLocationId) return Results.BadRequest("The item is already there.");
    if (!await IsLocationAvailable(request.DestinationLocationId, request.WorkspaceId, db))
        return Results.BadRequest("The destination is unavailable or archived.");

    await using var transaction = await db.Database.BeginTransactionAsync();
    db.ItemMovements.Add(new ItemMovement
    {
        WorkspaceId = request.WorkspaceId,
        ItemId = item.Id,
        FromLocationId = item.LocationId,
        ToLocationId = request.DestinationLocationId
    });
    item.LocationId = request.DestinationLocationId;
    await db.SaveChangesAsync();
    await transaction.CommitAsync();
    return Results.Ok(new ItemResponse(item.Id, item.WorkspaceId, item.LocationId, item.Name, item.Quantity, item.IsMoveLocked, item.IsEditLocked, item.Note, item.CreatedAt));
});
items.MapGet("/{id:guid}/history", async (Guid id, Guid workspaceId, InventoryDbContext db) =>
{
    if (!await db.Items.AnyAsync(x => x.Id == id && x.WorkspaceId == workspaceId)) return Results.NotFound();
    return Results.Ok(await (
        from movement in db.ItemMovements.AsNoTracking()
        join fromLocation in db.Locations.AsNoTracking() on movement.FromLocationId equals fromLocation.Id
        join toLocation in db.Locations.AsNoTracking() on movement.ToLocationId equals toLocation.Id
        where movement.WorkspaceId == workspaceId && movement.ItemId == id
        orderby movement.MovedAt descending
        select new MovementHistoryResponse(movement.Id, fromLocation.Id, fromLocation.Name, toLocation.Id, toLocation.Name, movement.MovedAt)
    ).ToListAsync());
});
items.MapDelete("/{id:guid}", async (Guid id, Guid workspaceId, InventoryDbContext db, IConfiguration configuration) =>
{
    var item = await db.Items.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == workspaceId);
    if (item is null) return Results.NotFound();
    var photoFiles = await db.Photos.Where(x => x.WorkspaceId == workspaceId && x.ItemId == id).Select(x => x.StoredFileName).ToListAsync();
    db.Items.Remove(item);
    await db.SaveChangesAsync();
    foreach (var file in photoFiles) { var path = Path.Combine(GetPhotoStoragePath(configuration), file); if (File.Exists(path)) File.Delete(path); }
    return Results.NoContent();
});

var photos = app.MapGroup("/api/photos");
photos.MapGet("/", async (Guid workspaceId, Guid? itemId, Guid? locationId, InventoryDbContext db) =>
{
    if ((itemId is null) == (locationId is null)) return Results.BadRequest("Choose exactly one item or location.");
    var query = db.Photos.AsNoTracking().Where(x => x.WorkspaceId == workspaceId);
    query = itemId is Guid item ? query.Where(x => x.ItemId == item) : query.Where(x => x.LocationId == locationId);
    return Results.Ok(await query.OrderByDescending(x => x.CreatedAt)
        .Select(x => new PhotoResponse(x.Id, x.WorkspaceId, x.ItemId, x.LocationId, x.OriginalFileName, x.ContentType, x.SizeBytes, x.Caption, x.CreatedAt, $"/api/photos/{x.Id}/content?workspaceId={workspaceId}"))
        .ToListAsync());
});
photos.MapPost("/", async (HttpRequest request, InventoryDbContext db, IConfiguration configuration) =>
{
    if (!request.HasFormContentType) return Results.BadRequest("A multipart photo upload is required.");
    var form = await request.ReadFormAsync();
    if (!Guid.TryParse(form["workspaceId"], out var workspaceId)) return Results.BadRequest("A valid workspace is required.");
    Guid? itemId = Guid.TryParse(form["itemId"], out var parsedItemId) ? parsedItemId : null;
    Guid? locationId = Guid.TryParse(form["locationId"], out var parsedLocationId) ? parsedLocationId : null;
    if ((itemId is null) == (locationId is null)) return Results.BadRequest("Choose exactly one item or location.");
    if (itemId is Guid item && !await db.Items.AnyAsync(x => x.Id == item && x.WorkspaceId == workspaceId)) return Results.NotFound("Item not found.");
    if (locationId is Guid location && !await db.Locations.AnyAsync(x => x.Id == location && x.WorkspaceId == workspaceId)) return Results.NotFound("Location not found.");
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0) return Results.BadRequest("Choose a photo.");
    var maxSize = configuration.GetValue<long?>("PhotoStorage:MaxFileSizeBytes") ?? 15 * 1024 * 1024;
    if (file.Length > maxSize) return Results.BadRequest($"Photos must be smaller than {maxSize / 1024 / 1024} MB.");
    var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp", "image/heic", "image/heif" };
    if (!allowedTypes.Contains(file.ContentType)) return Results.BadRequest("Use a JPEG, PNG, WebP, HEIC, or HEIF image.");
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (extension.Length > 10 || extension.Any(c => !char.IsLetterOrDigit(c) && c != '.')) extension = "";
    var storedFileName = $"{Guid.NewGuid():N}{extension}";
    var storagePath = GetPhotoStoragePath(configuration);
    Directory.CreateDirectory(storagePath);
    var fullPath = Path.Combine(storagePath, storedFileName);
    await using (var stream = File.Create(fullPath)) await file.CopyToAsync(stream);
    var photo = new InventoryPhoto { WorkspaceId = workspaceId, ItemId = itemId, LocationId = locationId, StoredFileName = storedFileName, OriginalFileName = Path.GetFileName(file.FileName), ContentType = file.ContentType, SizeBytes = file.Length, Caption = CleanCaption(form["caption"]) };
    try { db.Photos.Add(photo); await db.SaveChangesAsync(); }
    catch { File.Delete(fullPath); throw; }
    return Results.Created($"/api/photos/{photo.Id}", ToPhotoResponse(photo));
}).DisableAntiforgery();
photos.MapGet("/{id:guid}/content", async (Guid id, Guid workspaceId, InventoryDbContext db, IConfiguration configuration) =>
{
    var photo = await db.Photos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == workspaceId);
    if (photo is null) return Results.NotFound();
    var path = Path.Combine(GetPhotoStoragePath(configuration), photo.StoredFileName);
    if (!File.Exists(path)) return Results.NotFound("The photo file is missing from storage.");
    return Results.File(path, photo.ContentType, enableRangeProcessing: true);
});
photos.MapPatch("/{id:guid}", async (Guid id, UpdatePhotoRequest request, InventoryDbContext db) =>
{
    var photo = await db.Photos.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == request.WorkspaceId);
    if (photo is null) return Results.NotFound();
    photo.Caption = CleanCaption(request.Caption);
    await db.SaveChangesAsync();
    return Results.Ok(ToPhotoResponse(photo));
});
photos.MapDelete("/{id:guid}", async (Guid id, Guid workspaceId, InventoryDbContext db, IConfiguration configuration) =>
{
    var photo = await db.Photos.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == workspaceId);
    if (photo is null) return Results.NotFound();
    db.Photos.Remove(photo);
    await db.SaveChangesAsync();
    var path = Path.Combine(GetPhotoStoragePath(configuration), photo.StoredFileName);
    if (File.Exists(path)) File.Delete(path);
    return Results.NoContent();
});

locations.MapDelete("/{id:guid}", async (Guid id, Guid workspaceId, InventoryDbContext db) =>
{
    var location = await db.Locations.FirstOrDefaultAsync(x => x.Id == id && x.WorkspaceId == workspaceId);
    if (location is null) return Results.NotFound();
    var impact = await GetLocationDeletionImpact(id, workspaceId, db);
    if (!impact.CanDelete) return Results.Conflict(impact);

    db.Locations.Remove(location);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// React owns client-side routes; API endpoints above retain priority.
app.MapFallbackToFile("index.html");

app.Run();

static string GetPhotoStoragePath(IConfiguration configuration) =>
    Path.GetFullPath(configuration["PhotoStorage:Path"] ?? Path.Combine(AppContext.BaseDirectory, "photos"));

static string? CleanCaption(string? caption) => string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();

static PhotoResponse ToPhotoResponse(InventoryPhoto photo) => new(photo.Id, photo.WorkspaceId, photo.ItemId, photo.LocationId, photo.OriginalFileName, photo.ContentType, photo.SizeBytes, photo.Caption, photo.CreatedAt, $"/api/photos/{photo.Id}/content?workspaceId={photo.WorkspaceId}");

static bool IsDevelopmentClientOrigin(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Port != 5173) return false;
    if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
    if (!IPAddress.TryParse(uri.Host, out var address)) return false;
    if (IPAddress.IsLoopback(address)) return true;
    var bytes = address.GetAddressBytes();
    return bytes.Length == 4 && (bytes[0] == 10 || (bytes[0] == 192 && bytes[1] == 168) || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31));
}

static async Task<LocationDeletionImpactResponse> GetLocationDeletionImpact(Guid id, Guid workspaceId, InventoryDbContext db)
{
    var isRoot = await db.Locations.AnyAsync(x => x.Id == id && x.WorkspaceId == workspaceId && x.ParentId == null);
    var childCount = await db.Locations.CountAsync(x => x.WorkspaceId == workspaceId && x.ParentId == id);
    var itemCount = await db.Items.CountAsync(x => x.WorkspaceId == workspaceId && x.LocationId == id);
    var photoCount = await db.Photos.CountAsync(x => x.WorkspaceId == workspaceId && x.LocationId == id);
    var itemMovementCount = await db.ItemMovements.CountAsync(x => x.WorkspaceId == workspaceId && (x.FromLocationId == id || x.ToLocationId == id));
    var locationMovementCount = await db.LocationMovements.CountAsync(x => x.WorkspaceId == workspaceId && (x.LocationId == id || x.FromParentId == id || x.ToParentId == id));
    var movementCount = itemMovementCount + locationMovementCount;
    return new LocationDeletionImpactResponse(id, isRoot, childCount, itemCount, photoCount, movementCount,
        !isRoot && childCount == 0 && itemCount == 0 && photoCount == 0 && movementCount == 0);
}

static async Task<bool> IsLocationAvailable(Guid id, Guid workspaceId, InventoryDbContext db)
{
    Guid? cursor = id;
    while (cursor is Guid cursorId)
    {
        var location = await db.Locations.AsNoTracking()
            .Where(x => x.Id == cursorId && x.WorkspaceId == workspaceId)
            .Select(x => new { x.ParentId, x.Status })
            .FirstOrDefaultAsync();
        if (location is null || location.Status == LocationStatus.Inactive) return false;
        cursor = location.ParentId;
    }
    return true;
}
