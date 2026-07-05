namespace ShelfScout.Api.Domain;

/// <summary>
/// Computes an Item's lifecycle Status (ADR 0003). Pure — no I/O, never persisted.
/// </summary>
public static class ItemStatusCalculator
{
    public static ItemStatus ComputeStatus(Item item, DateOnly today, int resolvedThreshold)
    {
        if (item.RemovedAt is not null)
        {
            return item.RemovalReason == RemovalReason.Consumed
                ? ItemStatus.Consumed
                : ItemStatus.Discarded;
        }

        if (item.ExpiryDate < today)
        {
            return ItemStatus.Expired;
        }

        if (item.ExpiryDate <= today.AddDays(resolvedThreshold))
        {
            return ItemStatus.ExpiringSoon;
        }

        return ItemStatus.Fresh;
    }
}
