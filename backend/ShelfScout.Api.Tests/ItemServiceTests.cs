using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShelfScout.Api.Domain;

namespace ShelfScout.Api.Tests;

public class ItemServiceTests : IClassFixture<PostgresApiFactory>
{
    private readonly PostgresApiFactory _factory;

    public ItemServiceTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateItem_creates_one_row_scoped_to_the_household_and_location()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var (itemService, household, creator) = await SetUpAsync(scope, ct);
        var location = household.Locations[0];

        var item = await itemService.CreateItemAsync(
            household.Id, location.Id, "Yogurt", DateOnly.FromDateTime(DateTime.UtcNow), creator.Id, ct: ct);

        Assert.Equal(household.Id, item.HouseholdId);
        Assert.Equal(location.Id, item.LocationId);
        Assert.Equal(creator.Id, item.CreatedBy);
        Assert.Null(item.RemovedAt);
        Assert.Null(item.CategoryId);
    }

    [Fact]
    public async Task GetActiveInventory_excludes_removed_items()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var (itemService, household, creator) = await SetUpAsync(scope, ct);
        var location = household.Locations[0];
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var active = await itemService.CreateItemAsync(household.Id, location.Id, "Active", today, creator.Id, ct: ct);
        var removed = await itemService.CreateItemAsync(household.Id, location.Id, "Removed", today, creator.Id, ct: ct);
        removed.RemovedAt = DateTimeOffset.UtcNow;
        removed.RemovalReason = RemovalReason.Consumed;
        await db.SaveChangesAsync(ct);

        var activeInventory = await itemService.GetActiveInventoryAsync(household.Id, ct);

        Assert.Contains(activeInventory, i => i.Id == active.Id);
        Assert.DoesNotContain(activeInventory, i => i.Id == removed.Id);
    }

    [Fact]
    public async Task GetComingDue_returns_only_expired_and_expiring_soon_active_items()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var (itemService, household, creator) = await SetUpAsync(scope, ct);
        var fridge = household.Locations[0]; // threshold 3
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var expired = await itemService.CreateItemAsync(household.Id, fridge.Id, "Expired", today.AddDays(-1), creator.Id, ct: ct);
        var expiringSoon = await itemService.CreateItemAsync(household.Id, fridge.Id, "Expiring Soon", today.AddDays(3), creator.Id, ct: ct);
        var fresh = await itemService.CreateItemAsync(household.Id, fridge.Id, "Fresh", today.AddDays(4), creator.Id, ct: ct);

        var comingDue = await itemService.GetComingDueAsync(household.Id, today, ct);

        var comingDueIds = comingDue.Select(i => i.Id).ToList();
        Assert.Contains(expired.Id, comingDueIds);
        Assert.Contains(expiringSoon.Id, comingDueIds);
        Assert.DoesNotContain(fresh.Id, comingDueIds);
    }

    [Fact]
    public async Task Household_isolation_hides_items_from_another_household()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var (itemService, householdA, creatorA) = await SetUpAsync(scope, ct);
        var (_, householdB, creatorB) = await SetUpAsync(scope, ct);

        await itemService.CreateItemAsync(
            householdA.Id, householdA.Locations[0].Id, "A's Milk", DateOnly.FromDateTime(DateTime.UtcNow), creatorA.Id, ct: ct);
        var bItem = await itemService.CreateItemAsync(
            householdB.Id, householdB.Locations[0].Id, "B's Milk", DateOnly.FromDateTime(DateTime.UtcNow), creatorB.Id, ct: ct);

        var householdAInventory = await itemService.GetActiveInventoryAsync(householdA.Id, ct);
        var householdAComingDue = await itemService.GetComingDueAsync(householdA.Id, DateOnly.FromDateTime(DateTime.UtcNow), ct);

        Assert.DoesNotContain(householdAInventory, i => i.Id == bItem.Id);
        Assert.DoesNotContain(householdAComingDue, i => i.Id == bItem.Id);
    }

    [Fact]
    public async Task CreateItem_rejects_a_location_that_belongs_to_a_different_household()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var (itemService, householdA, creatorA) = await SetUpAsync(scope, ct);
        var (_, householdB, _) = await SetUpAsync(scope, ct);

        await Assert.ThrowsAsync<InvalidOperationException>(() => itemService.CreateItemAsync(
            householdA.Id, householdB.Locations[0].Id, "Cross-household", DateOnly.FromDateTime(DateTime.UtcNow), creatorA.Id, ct: ct));
    }

    [Fact]
    public async Task ConsumeItem_sets_removed_at_and_consumed_reason()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var (itemService, household, creator) = await SetUpAsync(scope, ct);
        var location = household.Locations[0];
        var item = await itemService.CreateItemAsync(household.Id, location.Id, "Yogurt", DateOnly.FromDateTime(DateTime.UtcNow), creator.Id, ct: ct);

        var consumed = await itemService.ConsumeItemAsync(household.Id, item.Id, ct);

        Assert.NotNull(consumed.RemovedAt);
        Assert.Equal(RemovalReason.Consumed, consumed.RemovalReason);
    }

    [Fact]
    public async Task DiscardItem_sets_removed_at_and_discarded_reason()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var (itemService, household, creator) = await SetUpAsync(scope, ct);
        var location = household.Locations[0];
        var item = await itemService.CreateItemAsync(household.Id, location.Id, "Moldy Bread", DateOnly.FromDateTime(DateTime.UtcNow), creator.Id, ct: ct);

        var discarded = await itemService.DiscardItemAsync(household.Id, item.Id, ct);

        Assert.NotNull(discarded.RemovedAt);
        Assert.Equal(RemovalReason.Discarded, discarded.RemovalReason);
    }

    [Fact]
    public async Task RemoveItem_rejects_an_item_from_another_household()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var (itemService, householdA, _) = await SetUpAsync(scope, ct);
        var (_, householdB, creatorB) = await SetUpAsync(scope, ct);
        var itemB = await itemService.CreateItemAsync(
            householdB.Id, householdB.Locations[0].Id, "B's Milk", DateOnly.FromDateTime(DateTime.UtcNow), creatorB.Id, ct: ct);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => itemService.ConsumeItemAsync(householdA.Id, itemB.Id, ct));
    }

    [Fact]
    public async Task BulkRemove_marks_many_items_removed_in_one_call()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var (itemService, household, creator) = await SetUpAsync(scope, ct);
        var location = household.Locations[0];
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var itemA = await itemService.CreateItemAsync(household.Id, location.Id, "Onion", today, creator.Id, ct: ct);
        var itemB = await itemService.CreateItemAsync(household.Id, location.Id, "Carrot", today, creator.Id, ct: ct);
        var untouched = await itemService.CreateItemAsync(household.Id, location.Id, "Garlic", today, creator.Id, ct: ct);

        var removedCount = await itemService.BulkRemoveAsync(household.Id, [itemA.Id, itemB.Id], RemovalReason.Consumed, ct);

        Assert.Equal(2, removedCount);
        var reloadedUntouched = await db.Items.SingleAsync(i => i.Id == untouched.Id, ct);
        Assert.Null(reloadedUntouched.RemovedAt);
        Assert.All(
            await db.Items.Where(i => i.Id == itemA.Id || i.Id == itemB.Id).ToListAsync(ct),
            i =>
            {
                Assert.NotNull(i.RemovedAt);
                Assert.Equal(RemovalReason.Consumed, i.RemovalReason);
            });
    }

    [Fact]
    public async Task UndoRemoval_restores_a_removed_item_to_active_inventory()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var (itemService, household, creator) = await SetUpAsync(scope, ct);
        var location = household.Locations[0];
        var item = await itemService.CreateItemAsync(household.Id, location.Id, "Yogurt", DateOnly.FromDateTime(DateTime.UtcNow), creator.Id, ct: ct);
        await itemService.DiscardItemAsync(household.Id, item.Id, ct);

        var restored = await itemService.UndoRemovalAsync(household.Id, item.Id, ct);

        Assert.Null(restored.RemovedAt);
        Assert.Null(restored.RemovalReason);
        var activeInventory = await itemService.GetActiveInventoryAsync(household.Id, ct);
        Assert.Contains(activeInventory, i => i.Id == item.Id);
    }

    private static async Task<(ItemService ItemService, Household Household, User Creator)> SetUpAsync(
        IServiceScope scope, CancellationToken ct)
    {
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var householdService = scope.ServiceProvider.GetRequiredService<HouseholdService>();
        var itemService = scope.ServiceProvider.GetRequiredService<ItemService>();

        var uid = $"uid-item-{Guid.NewGuid()}";
        var creator = await identityResolver.ResolveAsync(uid, $"{uid}@example.com", "Creator", ct);
        var household = await householdService.CreateHouseholdAsync(creator.Id, $"House {uid}", ct);

        return (itemService, household, creator);
    }
}
