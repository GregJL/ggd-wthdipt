using Ggd.Api.Data;
using Ggd.Api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

// Transactional audit checks against a disposable relational database.
await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
connection.CreateFunction<string?, string?, int>("num_nonnulls", (a, b) => (a is null ? 0 : 1) + (b is null ? 0 : 1));
var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
await using var db = new InventoryDbContext(options);
await db.Database.EnsureCreatedAsync();
var workspace = new Workspace { Name = "Test home" };
var root = new Location { WorkspaceId = workspace.Id, Name = "Basement", Kind = LocationKind.Floor };
var shelf = new Location { WorkspaceId = workspace.Id, ParentId = root.Id, Name = "Shelf", Kind = LocationKind.Storage };
db.AddRange(workspace, root, shelf);
await db.SaveChangesAsync();
var item = new InventoryItem { WorkspaceId = workspace.Id, LocationId = shelf.Id, Name = "Drill", Quantity = 1 };
db.Items.Add(item); await db.SaveChangesAsync();
Check((await Latest()).Summary.Contains("Added item “Drill”"), "add is recorded");
Check((await Latest()).LocationPath == "Basement › Shelf", "full path snapshot");

var count = await db.ActivityEntries.CountAsync();
await db.SaveChangesAsync();
Check(await db.ActivityEntries.CountAsync() == count, "no-op saves produce no entries");
item.Name = "Red drill"; item.Quantity = 2;
await db.SaveChangesAsync();
var edited = await Latest();
Check(edited.Details.Contains("Name: Drill → Red drill") && edited.Details.Contains("Quantity: 1 → 2"), "before and after values");
item.IsEditLocked = true; await db.SaveChangesAsync();
Check((await Latest()).Summary == "Locked editing for item “Red drill”", "edit lock is distinct");
item.IsMoveLocked = true; await db.SaveChangesAsync();
Check((await Latest()).Summary == "Locked movement for item “Red drill”", "move lock is distinct");
item.IsMoveLocked = false; await db.SaveChangesAsync();
item.LocationId = root.Id; await db.SaveChangesAsync();
Check((await Latest()).Action == "moved" && (await Latest()).Details.Contains("Basement › Shelf → Basement"), "movement paths");

shelf.Status = LocationStatus.Inactive; await db.SaveChangesAsync();
Check((await Latest()).Action == "archived", "archive");
shelf.Status = LocationStatus.Active; await db.SaveChangesAsync();
Check((await Latest()).Action == "restored", "restore");
var photo = new InventoryPhoto { WorkspaceId = workspace.Id, ItemId = item.Id, StoredFileName = "test.jpg", OriginalFileName = "Drill.jpg", ContentType = "image/jpeg", Caption = "Before" };
db.Photos.Add(photo); await db.SaveChangesAsync();
Check((await Latest()).SubjectId == item.Id && (await Latest()).Action == "photo-added", "photo belongs to item's history");
photo.Caption = "After"; await db.SaveChangesAsync();
Check((await Latest()).Details.Contains("Caption: Before → After"), "caption history");
db.Photos.Remove(photo); await db.SaveChangesAsync();
Check((await Latest()).Action == "photo-deleted", "photo deletion");

// Parent views include existing history for current contents at every depth.
item.LocationId = shelf.Id; item.Note = "Parent history regression";
await db.SaveChangesAsync();
var otherFloor = new Location { WorkspaceId = workspace.Id, Name = "Other floor" };
db.Locations.Add(otherFloor); await db.SaveChangesAsync();
var rootActivity = await (await ActivityQueries.ForLocationAsync(db, workspace.Id, root.Id)).ToListAsync();
Check(rootActivity.Any(x => x.SubjectId == item.Id && x.Id == edited.Id), "parent includes existing item edits");
Check(rootActivity.Any(x => x.SubjectId == shelf.Id && x.Action == "archived"), "parent includes nested location actions");
Check(rootActivity.Any(x => x.SubjectId == item.Id && x.Action == "photo-added"), "parent includes item photos");
Check(rootActivity.All(x => x.SubjectId != otherFloor.Id), "unrelated locations excluded");
Check(!await (await ActivityQueries.ForLocationAsync(db, Guid.NewGuid(), root.Id)).AnyAsync(), "location history is workspace scoped");
var shelfActivity = await (await ActivityQueries.ForLocationAsync(db, workspace.Id, shelf.Id)).ToListAsync();
Check(shelfActivity.Any(x => x.SubjectId == item.Id && x.Action == "updated"), "immediate parent includes edits");
Check(shelfActivity.All(x => x.SubjectId != root.Id), "child does not include ancestor actions");

db.Items.Remove(item); await db.SaveChangesAsync();
db.ChangeTracker.Clear();
Check(await db.ActivityEntries.AnyAsync(x => x.SubjectId == item.Id && x.Action == "deleted"), "history survives item deletion");
Check(await db.ActivityEntries.AnyAsync(x => x.Id == edited.Id && x.SubjectName == "Red drill" && x.LocationPath == "Basement › Shelf"), "old name and path retained");

count = await db.ActivityEntries.CountAsync();
db.Locations.Add(new Location { WorkspaceId = workspace.Id, ParentId = root.Id, Name = "Shelf" });
try { await db.SaveChangesAsync(); throw new Exception("Expected unique constraint failure"); }
catch (DbUpdateException) { }
db.ChangeTracker.Clear();
Check(await db.ActivityEntries.CountAsync() == count, "failed save rolls back activity");
Console.WriteLine("PASS: adds, edits, no-op saves, independent locks, moves, archive/restore, photos, deleted history, snapshots, rollback, and parent/descendant activity scope.");

async Task<ActivityEntry> Latest() => await db.ActivityEntries.OrderByDescending(x => x.Id).FirstAsync();
static void Check(bool condition, string label) { if (!condition) throw new Exception($"FAIL: {label}"); }
