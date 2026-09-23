using Ggd.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ggd.Api.Data;

public static class DbSeeder
{
    public static readonly Guid WorkspaceId = Id(1);
    public static readonly Guid GregUserId = Id(2);

    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        if (await db.Workspaces.AnyAsync(x => x.Id == WorkspaceId)) return;

        var workspace = new Workspace { Id = WorkspaceId, Name = "Greg's Home" };
        var greg = new AppUser
        {
            Id = GregUserId,
            Email = "greg@local.ggd",
            DisplayName = "Greg"
        };

        db.Workspaces.Add(workspace);
        db.Users.Add(greg);
        db.WorkspaceMembers.Add(new WorkspaceMember
        {
            Id = Id(3),
            UserId = greg.Id,
            WorkspaceId = workspace.Id,
            Role = "Owner"
        });

        var ground = Location(100, null, "Ground Floor", LocationKind.Floor);
        var basement = Location(200, null, "Basement", LocationKind.Floor);

        db.Locations.AddRange(
            ground,
            basement,
            Location(101, ground.Id, "Master Bedroom", LocationKind.Room),
            Location(102, ground.Id, "Master Bath", LocationKind.Room),
            Location(103, ground.Id, "Kitchen", LocationKind.Room),
            Location(104, ground.Id, "Dining Room", LocationKind.Room),
            Location(105, ground.Id, "Living Room", LocationKind.Room),
            Location(106, ground.Id, "Front Bedroom", LocationKind.Room),
            Location(107, ground.Id, "Rear Bedroom", LocationKind.Room),
            Location(108, ground.Id, "Guest Bathroom", LocationKind.Room),
            Location(109, ground.Id, "Sunroom", LocationKind.Room),
            Location(110, ground.Id, "Garage", LocationKind.Room)
        );

        var heatPress = Location(201, basement.Id, "North-Side Heat Press Area", LocationKind.Area);
        var workshop = Location(202, basement.Id, "South-Side Workshop", LocationKind.Area);
        var gym = Location(203, basement.Id, "South-Side Home Gym", LocationKind.Area);
        var manCave = Location(204, basement.Id, "South-Side Man Cave", LocationKind.Area);
        var office = Location(205, basement.Id, "South-Side Office Nook", LocationKind.Area);
        var underStairs = Location(206, basement.Id, "Under the Stairs", LocationKind.Area);
        var pelletStorage = Location(207, basement.Id, "Pellet Storage", LocationKind.Storage);
        var bar = Location(208, basement.Id, "South-Side Bar", LocationKind.Area,
            LocationStatus.Planned, "Do not inventory the bar before it is built.");

        db.Locations.AddRange(heatPress, workshop, gym, manCave, office, underStairs, pelletStorage, bar);
        db.Locations.AddRange(
            Location(301, workshop.Id, "Workbench", LocationKind.Storage),
            Location(302, workshop.Id, "Overflow Table", LocationKind.Storage, LocationStatus.Temporary),
            Location(303, workshop.Id, "Pegboard Tool Wall", LocationKind.Storage),
            Location(304, workshop.Id, "Red Toolbox", LocationKind.Storage),
            Location(305, workshop.Id, "Parts Organizers", LocationKind.Storage),
            Location(306, office.Id, "Divider Cabinets", LocationKind.Storage),
            Location(307, office.Id, "Wire Shelving", LocationKind.Storage),
            Location(308, heatPress.Id, "Wire Shelving", LocationKind.Storage),
            Location(309, heatPress.Id, "Tall Cabinets", LocationKind.Storage)
        );

        await db.SaveChangesAsync();
    }

    private static Location Location(
        int id,
        Guid? parentId,
        string name,
        LocationKind kind,
        LocationStatus status = LocationStatus.Active,
        string? note = null) => new()
        {
            Id = Id(id),
            WorkspaceId = WorkspaceId,
            ParentId = parentId,
            Name = name,
            Kind = kind,
            Status = status,
            IsMoveLocked = kind is LocationKind.Floor or LocationKind.Room,
            IsEditLocked = false,
            Note = note
        };

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}
