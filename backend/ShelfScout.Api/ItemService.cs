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

    /// <summary>
    /// The single removal operation (ADR 0003): records both <c>removed_at</c> and
    /// <c>removal_reason</c>. Consume and Discard are this with a fixed reason.
    /// </summary>
    public async Task<Item> RemoveItemAsync(
        int householdId,
        int itemId,
        RemovalReason reason,
        CancellationToken ct = default)
    {
        var item = await GetOwnedItemAsync(householdId, itemId, ct);

        item.RemovedAt = DateTimeOffset.UtcNow;
        item.RemovalReason = reason;

        await db.SaveChangesAsync(ct);
        return item;
    }

    public Task<Item> ConsumeItemAsync(int householdId, int itemId, CancellationToken ct = default) =>
        RemoveItemAsync(householdId, itemId, RemovalReason.Consumed, ct);

    public Task<Item> DiscardItemAsync(int householdId, int itemId, CancellationToken ct = default) =>
        RemoveItemAsync(householdId, itemId, RemovalReason.Discarded, ct);

    /// <summary>
    /// Marks many Items removed in one call (Bulk Consume, ADR 0016) — one <paramref name="reason"/>
    /// per action, so a mixed eat-some/toss-some selection is two calls.
    /// </summary>
    public async Task<int> BulkRemoveAsync(
        int householdId,
        IReadOnlyCollection<int> itemIds,
        RemovalReason reason,
        CancellationToken ct = default)
    {
        var items = await db.Items
            .Where(i => i.HouseholdId == householdId && itemIds.Contains(i.Id))
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var item in items)
        {
            item.RemovedAt = now;
            item.RemovalReason = reason;
        }

        await db.SaveChangesAsync(ct);
        return items.Count;
    }

    /// <summary>
    /// Restores a removed Item to active inventory (<c>removed_at</c> → NULL). Only meaningful
    /// within the 90-day Disposition Retention window (ADR 0004) — beyond that the row is
    /// already hard-deleted by the retention sweep, so no separate window check is needed here.
    /// </summary>
    public async Task<Item> UndoRemovalAsync(int householdId, int itemId, CancellationToken ct = default)
    {
        var item = await GetOwnedItemAsync(householdId, itemId, ct);

        item.RemovedAt = null;
        item.RemovalReason = null;

        await db.SaveChangesAsync(ct);
        return item;
    }

    private async Task<Item> GetOwnedItemAsync(int householdId, int itemId, CancellationToken ct)
    {
        var item = await db.Items.SingleOrDefaultAsync(i => i.Id == itemId && i.HouseholdId == householdId, ct);

        if (item is null)
        {
            throw new InvalidOperationException($"Item {itemId} does not belong to household {householdId}.");
        }

        return item;
    }
}
