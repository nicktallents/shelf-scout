namespace ShelfScout.Api.Domain;

/// <summary>
/// Durable, append/increment-only rollup that outlives 90-day Item detail (ADR 0004, ADR 0019).
/// Grain: household x period-month x location_kind x removal_reason x category label. The
/// category slot is a denormalized, never-rewritten label (plus <see cref="CategoryIsGlobal"/>
/// to keep global and household-local labels distinguishable) so a row still renders after its
/// source Category is deleted or renamed.
/// </summary>
public class ConsumptionStat
{
    public int Id { get; set; }

    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;

    /// <summary>First-of-month; the calendar month of the folded Item's <c>removed_at</c>.</summary>
    public DateOnly PeriodMonth { get; set; }

    public LocationKind? LocationKind { get; set; }
    public RemovalReason RemovalReason { get; set; }

    /// <summary>Durable category label, "Uncategorized" for Items with no category at fold time.</summary>
    public string CategoryLabel { get; set; } = null!;
    public bool CategoryIsGlobal { get; set; }

    public int Count { get; set; }
}
