using Microsoft.EntityFrameworkCore;
using ShelfScout.Api.Domain;

namespace ShelfScout.Api;

/// <summary>
/// Dev-only sample data (ADR 0021): a household, a membership for the development fallback
/// user, locations, and items whose expiry dates are computed relative to <c>now</c> so every
/// fresh boot lands a stable spread across the report buckets. Gated on <c>IsDevelopment()</c>
/// by the caller — never run in Testing or Production.
/// </summary>
public class DevDataSeeder(ShelfScoutDbContext db, IdentityResolver identityResolver, HouseholdService householdService, ItemService itemService)
{
    public const string HouseholdName = "Sample Household";

    /// <summary>Idempotently seeds the fixture. Safe to call on every startup.</summary>
    public async Task SeedAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var user = await identityResolver.ResolveAsync(
            CurrentUser.DevelopmentFallbackUid,
            CurrentUser.DevelopmentFallbackEmail,
            CurrentUser.DevelopmentFallbackDisplayName,
            ct);

        var alreadySeeded = await db.Memberships
            .AnyAsync(m => m.UserId == user.Id && m.Household.Name == HouseholdName, ct);

        if (alreadySeeded)
        {
            return;
        }

        var household = await householdService.CreateHouseholdAsync(user.Id, HouseholdName, ct);

        var fridge = household.Locations.Single(l => l.Kind == LocationKind.Fridge);
        var freezer = household.Locations.Single(l => l.Kind == LocationKind.Freezer);
        var pantry = household.Locations.Single(l => l.Kind == LocationKind.Pantry);

        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var fixtures = new (string Name, Location Location, DateOnly ExpiryDate)[]
        {
            ("Milk", fridge, today.AddDays(-2)),
            ("Yogurt", fridge, today.AddDays(2)),
            ("Leftover Pasta", fridge, today.AddDays(10)),
            ("Bread", pantry, today.AddDays(1)),
            ("Canned Beans", pantry, today.AddDays(180)),
            ("Frozen Peas", freezer, today.AddDays(60)),
        };

        foreach (var fixture in fixtures)
        {
            await itemService.CreateItemAsync(
                household.Id, fixture.Location.Id, fixture.Name, fixture.ExpiryDate, user.Id, ct: ct);
        }
    }
}
