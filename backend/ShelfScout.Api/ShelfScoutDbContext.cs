using Microsoft.EntityFrameworkCore;
using ShelfScout.Api.Domain;

namespace ShelfScout.Api;

public class ShelfScoutDbContext(DbContextOptions<ShelfScoutDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Identity> Identities => Set<Identity>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ConsumptionStat> ConsumptionStats => Set<ConsumptionStat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>();

        modelBuilder.Entity<Identity>(entity =>
        {
            entity.HasIndex(i => new { i.Provider, i.Subject }).IsUnique();
            entity.HasOne(i => i.User)
                .WithMany(u => u.Identities)
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Household>();

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.HasKey(m => new { m.UserId, m.HouseholdId });

            entity.Property(m => m.Role)
                .HasConversion(
                    role => role.ToString().ToLowerInvariant(),
                    value => Enum.Parse<Role>(value, ignoreCase: true));

            entity.HasOne(m => m.User)
                .WithMany(u => u.Memberships)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.Household)
                .WithMany(h => h.Memberships)
                .HasForeignKey(m => m.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.Property(l => l.Kind)
                .HasConversion(
                    kind => kind == null ? null : kind.Value.ToString().ToLowerInvariant(),
                    value => value == null ? null : Enum.Parse<LocationKind>(value, ignoreCase: true));

            entity.HasOne(l => l.Household)
                .WithMany(h => h.Locations)
                .HasForeignKey(l => l.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasOne(c => c.Household)
                .WithMany()
                .HasForeignKey(c => c.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            // Global categories (household_id NULL) are unique by name; custom categories are
            // unique by name within their household. Two filtered indexes because Postgres
            // treats each NULL as distinct in a plain composite unique index.
            entity.HasIndex(c => c.Name)
                .IsUnique()
                .HasFilter("household_id IS NULL");

            entity.HasIndex(c => new { c.HouseholdId, c.Name })
                .IsUnique()
                .HasFilter("household_id IS NOT NULL");
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.Property(i => i.RemovalReason)
                .HasConversion(
                    reason => reason == null ? null : reason.Value.ToString().ToLowerInvariant(),
                    value => value == null ? null : Enum.Parse<RemovalReason>(value, ignoreCase: true));

            entity.HasOne(i => i.Household)
                .WithMany(h => h.Items)
                .HasForeignKey(i => i.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.Location)
                .WithMany(l => l.Items)
                .HasForeignKey(i => i.LocationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deleting a custom Category nulls referencing Items' category_id (global
            // categories are never deleted, so this only ever fires for custom ones).
            entity.HasOne(i => i.Category)
                .WithMany()
                .HasForeignKey(i => i.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ConsumptionStat>(entity =>
        {
            entity.Property(s => s.LocationKind)
                .HasConversion(
                    kind => kind == null ? null : kind.Value.ToString().ToLowerInvariant(),
                    value => value == null ? null : Enum.Parse<LocationKind>(value, ignoreCase: true));

            entity.Property(s => s.RemovalReason)
                .HasConversion(
                    reason => reason.ToString().ToLowerInvariant(),
                    value => Enum.Parse<RemovalReason>(value, ignoreCase: true));

            entity.HasOne(s => s.Household)
                .WithMany()
                .HasForeignKey(s => s.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            // The increment grain (ADR 0004/0019): one row per household x month x
            // location_kind x removal_reason x category label, distinguishing global vs
            // household-local labels so same-named custom/global categories never collide.
            entity.HasIndex(s => new
            {
                s.HouseholdId,
                s.PeriodMonth,
                s.LocationKind,
                s.RemovalReason,
                s.CategoryLabel,
                s.CategoryIsGlobal,
            }).IsUnique();
        });
    }
}
