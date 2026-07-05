namespace ShelfScout.Api.Domain;

/// <summary>
/// The single source for seed thresholds and household defaults. Changing values here only
/// affects newly created Households (see ADR 0005) — existing Household/Location rows own
/// their own values and are never retuned by a config change.
/// </summary>
public static class SeedConfig
{
    public const int DefaultHouseholdAlertThresholdDays = 3;

    /// <summary>
    /// Disposition Retention window (ADR 0004): a Removed Item survives this many days before
    /// the periodic sweep folds it into a Consumption Stat and hard-deletes it.
    /// </summary>
    public const int DispositionRetentionDays = 90;

    public static readonly IReadOnlyList<SeedLocation> DefaultLocations =
    [
        new("Fridge", LocationKind.Fridge, 3),
        new("Freezer", LocationKind.Freezer, 30),
        new("Pantry", LocationKind.Pantry, 14),
    ];

    public readonly record struct SeedLocation(string Name, LocationKind Kind, int AlertThresholdDays);

    /// <summary>
    /// The spine's definition of a Meal (CONTEXT.md "Leftover Marker"): a global, system-seeded
    /// Category the Leftover Marker sets at capture time. Looked up by name since global
    /// Categories are seeded once and household-immutable.
    /// </summary>
    public const string LeftoversCategoryName = "Prepared / Leftovers";

    public static readonly IReadOnlyList<string> GlobalCategoryNames =
    [
        "Dairy",
        "Meat & Poultry",
        "Produce",
        "Bakery",
        "Pantry Staples",
        "Beverages",
        LeftoversCategoryName,
        "Other",
    ];
}
