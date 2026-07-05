using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShelfScout.Api.Domain;

namespace ShelfScout.Api.Tests;

public class LocationServiceTests : IClassFixture<PostgresApiFactory>
{
    private readonly PostgresApiFactory _factory;

    public LocationServiceTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateLocation_scopes_it_to_the_household()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var (locationService, household, _) = await SetUpAsync(scope, ct);

        var location = await locationService.CreateLocationAsync(household.Id, "Garage Freezer", LocationKind.Freezer, 60, ct);

        Assert.Equal(household.Id, location.HouseholdId);
        Assert.Equal(LocationKind.Freezer, location.Kind);
        Assert.Equal(60, location.AlertThresholdDays);
    }

    [Fact]
    public async Task UpdateLocation_renames_and_edits_kind_and_threshold()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var (locationService, household, _) = await SetUpAsync(scope, ct);
        var location = await locationService.CreateLocationAsync(household.Id, "Spare Fridge", LocationKind.Fridge, 3, ct);

        var updated = await locationService.UpdateLocationAsync(
            household.Id, location.Id, "Basement Fridge", LocationKind.Other, 10, ct);

        Assert.Equal("Basement Fridge", updated.Name);
        Assert.Equal(LocationKind.Other, updated.Kind);
        Assert.Equal(10, updated.AlertThresholdDays);
    }

    [Fact]
    public async Task UpdateLocation_rejects_a_location_from_another_household()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var (locationService, householdA, _) = await SetUpAsync(scope, ct);
        var (_, householdB, _) = await SetUpAsync(scope, ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => locationService.UpdateLocationAsync(
            householdA.Id, householdB.Locations[0].Id, "Hijacked", null, null, ct));
    }

    [Fact]
    public async Task DeleteLocation_deletes_an_empty_location_immediately()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var (locationService, household, _) = await SetUpAsync(scope, ct);
        var location = await locationService.CreateLocationAsync(household.Id, "Empty Pantry Shelf", ct: ct);

        var result = await locationService.DeleteLocationAsync(household.Id, location.Id, confirm: false, ct: ct);

        Assert.True(result.Deleted);
        Assert.Equal(0, result.ItemCount);
        Assert.False(await db.Locations.AnyAsync(l => l.Id == location.Id, ct));
    }

    [Fact]
    public async Task DeleteLocation_without_confirm_returns_item_count_and_does_not_delete()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var itemService = scope.ServiceProvider.GetRequiredService<ItemService>();
        var (locationService, household, creator) = await SetUpAsync(scope, ct);
        var location = await locationService.CreateLocationAsync(household.Id, "Busy Shelf", ct: ct);
        await itemService.CreateItemAsync(household.Id, location.Id, "Milk", DateOnly.FromDateTime(DateTime.UtcNow), creator.Id, ct: ct);

        var result = await locationService.DeleteLocationAsync(household.Id, location.Id, confirm: false, ct: ct);

        Assert.False(result.Deleted);
        Assert.Equal(1, result.ItemCount);
        Assert.True(await db.Locations.AnyAsync(l => l.Id == location.Id, ct));
    }

    [Fact]
    public async Task DeleteLocation_with_confirm_cascade_deletes_its_items()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var itemService = scope.ServiceProvider.GetRequiredService<ItemService>();
        var (locationService, household, creator) = await SetUpAsync(scope, ct);
        var location = await locationService.CreateLocationAsync(household.Id, "Busy Shelf 2", ct: ct);
        var item = await itemService.CreateItemAsync(household.Id, location.Id, "Milk", DateOnly.FromDateTime(DateTime.UtcNow), creator.Id, ct: ct);

        var result = await locationService.DeleteLocationAsync(household.Id, location.Id, confirm: true, ct: ct);

        Assert.True(result.Deleted);
        Assert.Equal(1, result.ItemCount);
        Assert.False(await db.Locations.AnyAsync(l => l.Id == location.Id, ct));
        Assert.False(await db.Items.AnyAsync(i => i.Id == item.Id, ct));
    }

    private static async Task<(LocationService LocationService, Household Household, User Creator)> SetUpAsync(
        IServiceScope scope, CancellationToken ct)
    {
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var householdService = scope.ServiceProvider.GetRequiredService<HouseholdService>();
        var locationService = scope.ServiceProvider.GetRequiredService<LocationService>();

        var uid = $"uid-location-{Guid.NewGuid()}";
        var creator = await identityResolver.ResolveAsync(uid, $"{uid}@example.com", "Creator", ct);
        var household = await householdService.CreateHouseholdAsync(creator.Id, $"House {uid}", ct);

        return (locationService, household, creator);
    }
}
