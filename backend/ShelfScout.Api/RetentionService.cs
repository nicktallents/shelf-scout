using Microsoft.EntityFrameworkCore;
using ShelfScout.Api.Domain;

namespace ShelfScout.Api;

/// <summary>
/// The periodic Disposition Retention sweep (ADR 0004/0019): folds every Removed Item older
/// than <see cref="SeedConfig.DispositionRetentionDays"/> into its Consumption Stat grain, then
/// hard-deletes it. Removed Items within the window and Expired-but-not-Removed Items are left
/// untouched.
/// </summary>
public class RetentionService(ShelfScoutDbContext db)
{
    public async Task<int> SweepExpiredDispositionsAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var cutoff = now.AddDays(-SeedConfig.DispositionRetentionDays);

        var expiredItems = await db.Items
            .Include(i => i.Location)
            .Include(i => i.Category)
            .Where(i => i.RemovedAt != null && i.RemovedAt < cutoff)
            .ToListAsync(ct);

        if (expiredItems.Count == 0)
        {
            return 0;
        }

        var groups = expiredItems.GroupBy(i => new StatGrain(
            i.HouseholdId,
            new DateOnly(i.RemovedAt!.Value.Year, i.RemovedAt.Value.Month, 1),
            i.Location.Kind,
            i.RemovalReason!.Value,
            i.Category?.Name ?? "Uncategorized",
            i.Category?.IsGlobal ?? false));

        foreach (var group in groups)
        {
            var grain = group.Key;

            var stat = await db.ConsumptionStats.SingleOrDefaultAsync(s =>
                s.HouseholdId == grain.HouseholdId &&
                s.PeriodMonth == grain.PeriodMonth &&
                s.LocationKind == grain.LocationKind &&
                s.RemovalReason == grain.RemovalReason &&
                s.CategoryLabel == grain.CategoryLabel &&
                s.CategoryIsGlobal == grain.CategoryIsGlobal, ct);

            if (stat is null)
            {
                stat = new ConsumptionStat
                {
                    HouseholdId = grain.HouseholdId,
                    PeriodMonth = grain.PeriodMonth,
                    LocationKind = grain.LocationKind,
                    RemovalReason = grain.RemovalReason,
                    CategoryLabel = grain.CategoryLabel,
                    CategoryIsGlobal = grain.CategoryIsGlobal,
                    Count = 0,
                };
                db.ConsumptionStats.Add(stat);
            }

            stat.Count += group.Count();
        }

        db.Items.RemoveRange(expiredItems);
        await db.SaveChangesAsync(ct);
        return expiredItems.Count;
    }

    private readonly record struct StatGrain(
        int HouseholdId,
        DateOnly PeriodMonth,
        LocationKind? LocationKind,
        RemovalReason RemovalReason,
        string CategoryLabel,
        bool CategoryIsGlobal);
}
