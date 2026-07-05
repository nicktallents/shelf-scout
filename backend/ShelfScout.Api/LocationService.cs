using Microsoft.EntityFrameworkCore;
using ShelfScout.Api.Domain;

namespace ShelfScout.Api;

/// <summary>
/// Household-scoped Location CRUD (CONTEXT.md). Deleting an empty Location is immediate;
/// deleting a non-empty one requires an explicit confirm, then cascade-deletes its Items.
/// </summary>
public class LocationService(ShelfScoutDbContext db)
{
    public async Task<Location> CreateLocationAsync(
        int householdId,
        string name,
        LocationKind? kind = null,
        int? alertThresholdDays = null,
        CancellationToken ct = default)
    {
        var location = new Location
        {
            HouseholdId = householdId,
            Name = name,
            Kind = kind,
            AlertThresholdDays = alertThresholdDays,
        };

        db.Locations.Add(location);
        await db.SaveChangesAsync(ct);
        return location;
    }

    public async Task<Location> UpdateLocationAsync(
        int householdId,
        int locationId,
        string name,
        LocationKind? kind,
        int? alertThresholdDays,
        CancellationToken ct = default)
    {
        var location = await GetOwnedLocationAsync(householdId, locationId, ct);

        location.Name = name;
        location.Kind = kind;
        location.AlertThresholdDays = alertThresholdDays;

        await db.SaveChangesAsync(ct);
        return location;
    }

    /// <summary>
    /// Deletes an empty Location immediately. A non-empty Location is only deleted when
    /// <paramref name="confirm"/> is true (cascade-deleting its Items); otherwise this
    /// returns the Item count for the caller to show a confirmation prompt.
    /// </summary>
    public async Task<LocationDeletionResult> DeleteLocationAsync(
        int householdId,
        int locationId,
        bool confirm = false,
        CancellationToken ct = default)
    {
        var location = await GetOwnedLocationAsync(householdId, locationId, ct);

        var itemCount = await db.Items.CountAsync(i => i.LocationId == locationId, ct);

        if (itemCount > 0 && !confirm)
        {
            return new LocationDeletionResult(Deleted: false, ItemCount: itemCount);
        }

        db.Locations.Remove(location);
        await db.SaveChangesAsync(ct);
        return new LocationDeletionResult(Deleted: true, ItemCount: itemCount);
    }

    private async Task<Location> GetOwnedLocationAsync(int householdId, int locationId, CancellationToken ct)
    {
        var location = await db.Locations.SingleOrDefaultAsync(
            l => l.Id == locationId && l.HouseholdId == householdId, ct);

        if (location is null)
        {
            throw new InvalidOperationException(
                $"Location {locationId} does not belong to household {householdId}.");
        }

        return location;
    }
}

public readonly record struct LocationDeletionResult(bool Deleted, int ItemCount);
