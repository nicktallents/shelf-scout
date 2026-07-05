using ShelfScout.Api.Domain;

namespace ShelfScout.Api;

/// <summary>
/// Bootstraps a new Household: seeds Fridge/Freezer/Pantry Locations and binds the creator
/// as owner (CONTEXT.md, ADR 0005).
/// </summary>
public class HouseholdService(ShelfScoutDbContext db)
{
    public async Task<Household> CreateHouseholdAsync(int creatorUserId, string name, CancellationToken ct = default)
    {
        var household = new Household
        {
            Name = name,
            DefaultAlertThresholdDays = SeedConfig.DefaultHouseholdAlertThresholdDays,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        foreach (var seedLocation in SeedConfig.DefaultLocations)
        {
            household.Locations.Add(new Location
            {
                Name = seedLocation.Name,
                Kind = seedLocation.Kind,
                AlertThresholdDays = seedLocation.AlertThresholdDays,
            });
        }

        household.Memberships.Add(new Membership
        {
            UserId = creatorUserId,
            Role = Role.Owner,
            JoinedAt = DateTimeOffset.UtcNow,
        });

        db.Households.Add(household);
        await db.SaveChangesAsync(ct);
        return household;
    }
}
