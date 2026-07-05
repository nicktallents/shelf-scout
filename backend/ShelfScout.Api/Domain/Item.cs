namespace ShelfScout.Api.Domain;

public class Item
{
    public int Id { get; set; }

    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;

    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public string Name { get; set; } = null!;
    public DateOnly ExpiryDate { get; set; }

    // No FK yet — the categories table lands in a later chunk.
    public int? CategoryId { get; set; }

    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Disposition (ADR 0003): the only persisted lifecycle fact. NULL removed_at = active.
    public DateTimeOffset? RemovedAt { get; set; }
    public RemovalReason? RemovalReason { get; set; }
}
