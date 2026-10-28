using Ggd.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Ggd.Api.Data;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<InventoryItem> Items => Set<InventoryItem>();
    public DbSet<ItemMovement> ItemMovements => Set<ItemMovement>();
    public DbSet<LocationMovement> LocationMovements => Set<LocationMovement>();
    public DbSet<InventoryPhoto> Photos => Set<InventoryPhoto>();

    public DbSet<ActivityEntry> ActivityEntries => Set<ActivityEntry>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesAsync(acceptAllChangesOnSuccess, CancellationToken.None).GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var activity = await ActivityRecorder.CaptureAsync(this, cancellationToken);
        ActivityEntries.AddRange(activity);
        try
        {
            // The inventory change and its history commit in the same transaction.
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch
        {
            foreach (var entry in activity) Entry(entry).State = EntityState.Detached;
            throw;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActivityEntry>().HasIndex(x => new { x.WorkspaceId, x.Id });
        modelBuilder.Entity<ActivityEntry>().HasIndex(x => new { x.WorkspaceId, x.SubjectType, x.SubjectId, x.Id });
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<WorkspaceMember>().HasIndex(x => new { x.UserId, x.WorkspaceId }).IsUnique();
        modelBuilder.Entity<Location>().HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Location>().HasIndex(x => new { x.WorkspaceId, x.ParentId, x.Name }).IsUnique();
        modelBuilder.Entity<InventoryItem>().HasOne(x => x.Location).WithMany(x => x.Items).HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ItemMovement>().HasOne(x => x.Item).WithMany(x => x.Movements).HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LocationMovement>().HasOne(x => x.Location).WithMany(x => x.Movements).HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ItemMovement>().HasIndex(x => new { x.ItemId, x.MovedAt });
        modelBuilder.Entity<LocationMovement>().HasIndex(x => new { x.LocationId, x.MovedAt });
        modelBuilder.Entity<InventoryPhoto>().HasOne(x => x.Item).WithMany(x => x.Photos).HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<InventoryPhoto>().HasOne(x => x.Location).WithMany(x => x.Photos).HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<InventoryPhoto>().HasIndex(x => new { x.WorkspaceId, x.ItemId });
        modelBuilder.Entity<InventoryPhoto>().HasIndex(x => new { x.WorkspaceId, x.LocationId });
        modelBuilder.Entity<InventoryPhoto>().ToTable(table => table.HasCheckConstraint("CK_Photos_OneOwner", "num_nonnulls(\"ItemId\", \"LocationId\") = 1"));
    }
}
