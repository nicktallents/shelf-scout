using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    /// Owner-gated: adds a User to the Household with the given Role. Multiple owners are
    /// permitted (CONTEXT.md Role).
    /// </summary>
    public async Task<Membership> AddMemberAsync(
        int householdId,
        int actingUserId,
        int newUserId,
        Role role,
        CancellationToken ct = default)
    {
        await RequireOwnerAsync(householdId, actingUserId, ct);

        var alreadyMember = await db.Memberships
            .AnyAsync(m => m.HouseholdId == householdId && m.UserId == newUserId, ct);

        if (alreadyMember)
        {
            throw new InvalidOperationException(
                $"User {newUserId} is already a member of household {householdId}.");
        }

        var membership = new Membership
        {
            HouseholdId = householdId,
            UserId = newUserId,
            Role = role,
            JoinedAt = DateTimeOffset.UtcNow,
        };

        db.Memberships.Add(membership);
        await db.SaveChangesAsync(ct);
        return membership;
    }

    /// <summary>
    /// Owner-gated: removes only the target Membership row, leaving the User and their other
    /// Memberships intact (CONTEXT.md Membership).
    /// </summary>
    public async Task RemoveMemberAsync(
        int householdId,
        int actingUserId,
        int targetUserId,
        CancellationToken ct = default)
    {
        await RequireOwnerAsync(householdId, actingUserId, ct);

        var membership = await db.Memberships.SingleOrDefaultAsync(
            m => m.HouseholdId == householdId && m.UserId == targetUserId, ct);

        if (membership is null)
        {
            throw new InvalidOperationException(
                $"User {targetUserId} is not a member of household {householdId}.");
        }

        db.Memberships.Remove(membership);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Owner-gated: renames the Household.</summary>
    public async Task<Household> RenameHouseholdAsync(
        int householdId,
        int actingUserId,
        string name,
        CancellationToken ct = default)
    {
        await RequireOwnerAsync(householdId, actingUserId, ct);

        var household = await db.Households.SingleAsync(h => h.Id == householdId, ct);
        household.Name = name;

        await db.SaveChangesAsync(ct);
        return household;
    }

    /// <summary>
    /// Owner-gated: deletes the Household and cascades to all owned data — Memberships,
    /// Locations, Items, custom Categories, and Consumption Stats (all FK-cascaded).
    /// </summary>
    public async Task DeleteHouseholdAsync(int householdId, int actingUserId, CancellationToken ct = default)
    {
        await RequireOwnerAsync(householdId, actingUserId, ct);

        var household = await db.Households.SingleAsync(h => h.Id == householdId, ct);
        db.Households.Remove(household);
        await db.SaveChangesAsync(ct);
    }

    private async Task RequireOwnerAsync(int householdId, int actingUserId, CancellationToken ct)
    {
        var role = await db.Memberships
            .Where(m => m.HouseholdId == householdId && m.UserId == actingUserId)
            .Select(m => (Role?)m.Role)
            .SingleOrDefaultAsync(ct);

        if (role != Role.Owner)
        {
            throw new InvalidOperationException(
                $"User {actingUserId} is not an owner of household {householdId}.");
        }
    }
}
