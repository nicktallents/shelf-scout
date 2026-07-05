using Microsoft.EntityFrameworkCore;
using ShelfScout.Api.Domain;

namespace ShelfScout.Api;

/// <summary>
/// Household-scoped Item operations. Every read and write is scoped by household_id, making
/// cross-household access structurally impossible (CONTEXT.md).
/// </summary>
public class ItemService(ShelfScoutDbContext db)
{
    public async Task<Item> CreateItemAsync(
        int householdId,
        int locationId,
        string name,
        DateOnly expiryDate,
        int createdByUserId,
        int? categoryId = null,
        CancellationToken ct = default)
    {
        var locationBelongsToHousehold = await db.Locations
            .AnyAsync(l => l.Id == locationId && l.HouseholdId == householdId, ct);

        if (!locationBelongsToHousehold)
        {
            throw new InvalidOperationException(
                $"Location {locationId} does not belong to household {householdId}.");
        }

        var item = new Item
        {
            HouseholdId = householdId,
            LocationId = locationId,
            Name = name,
            ExpiryDate = expiryDate,
            CategoryId = categoryId,
            CreatedBy = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Items.Add(item);
        await db.SaveChangesAsync(ct);
        return item;
    }

    public Task<List<Item>> GetActiveInventoryAsync(int householdId, CancellationToken ct = default) =>
        db.Items
            .Where(i => i.HouseholdId == householdId && i.RemovedAt == null)
            .ToListAsync(ct);

    public async Task<List<Item>> GetComingDueAsync(int householdId, DateOnly today, CancellationToken ct = default)
    {
        var activeItems = await db.Items
            .Include(i => i.Location)
            .Include(i => i.Household)
            .Where(i => i.HouseholdId == householdId && i.RemovedAt == null)
            .ToListAsync(ct);

        return activeItems.Where(i => IsComingDue(i, today)).ToList();
    }

    private static bool IsComingDue(Item item, DateOnly today)
    {
        var threshold = ThresholdResolver.ResolveThreshold(item);
        var status = ItemStatusCalculator.ComputeStatus(item, today, threshold);
        return status is ItemStatus.Expired or ItemStatus.ExpiringSoon;
    }
}
