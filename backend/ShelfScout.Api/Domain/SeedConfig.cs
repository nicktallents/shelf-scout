namespace ShelfScout.Api.Domain;

/// <summary>
/// The single source for seed thresholds and household defaults. Changing values here only
/// affects newly created Households (see ADR 0005) — existing Household/Location rows own
/// their own values and are never retuned by a config change.
/// </summary>
public static class SeedConfig
{
    public const int DefaultHouseholdAlertThresholdDays = 3;

    public static readonly IReadOnlyList<SeedLocation> DefaultLocations =
    [
        new("Fridge", LocationKind.Fridge, 3),
        new("Freezer", LocationKind.Freezer, 30),
        new("Pantry", LocationKind.Pantry, 14),
    ];

    public readonly record struct SeedLocation(string Name, LocationKind Kind, int AlertThresholdDays);
}
