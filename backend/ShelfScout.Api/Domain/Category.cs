namespace ShelfScout.Api.Domain;

/// <summary>
/// Two-tier taxonomy (ADR 0004): global categories (HouseholdId null, system-seeded,
/// household-immutable) and custom categories (HouseholdId set, per-household CRUD).
/// </summary>
public class Category
{
    public int Id { get; set; }

    public int? HouseholdId { get; set; }
    public Household? Household { get; set; }

    public string Name { get; set; } = null!;

    public bool IsGlobal => HouseholdId is null;
}
