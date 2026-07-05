namespace ShelfScout.Api.Domain;

/// <summary>
/// Resolves the Alert Threshold that drives "Expiring Soon" (ADR 0005). Pure — no I/O; the
/// caller is responsible for having Location and Household loaded on the Item.
/// </summary>
public static class ThresholdResolver
{
    public static int ResolveThreshold(Item item) =>
        item.Location.AlertThresholdDays ?? item.Household.DefaultAlertThresholdDays;
}
