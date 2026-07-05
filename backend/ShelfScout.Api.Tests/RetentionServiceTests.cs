using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShelfScout.Api.Domain;

namespace ShelfScout.Api.Tests;

public class RetentionServiceTests : IClassFixture<PostgresApiFactory>
{
    private readonly PostgresApiFactory _factory;

    public RetentionServiceTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Sweep_folds_a_removed_item_older_than_90_days_into_its_consumption_stat_grain_and_deletes_it()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var retentionService = scope.ServiceProvider.GetRequiredService<RetentionService>();
        var (itemService, household, creator) = await SetUpAsync(scope, ct);
        var fridge = household.Locations.Single(l => l.Kind == LocationKind.Fridge);
        var category = scope.ServiceProvider.GetRequiredService<CategoryService>();
        var dairy = await category.CreateCustomCategoryAsync(household.Id, "Dairy", ct);

        var item = await itemService.CreateItemAsync(
            household.Id, fridge.Id, "Yogurt", DateOnly.FromDateTime(DateTime.UtcNow), creator.Id, categoryId: dairy.Id, ct: ct);
        var removedAt = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero);
        item.RemovedAt = removedAt;
        item.RemovalReason = RemovalReason.Discarded;
        await db.SaveChangesAsync(ct);

        var now = removedAt.AddDays(91);
        var swept = await retentionService.SweepExpiredDispositionsAsync(now, ct);

        Assert.Equal(1, swept);
        Assert.False(await db.Items.AnyAsync(i => i.Id == item.Id, ct));

        var stat = await db.ConsumptionStats.SingleAsync(s => s.HouseholdId == household.Id, ct);
        Assert.Equal(new DateOnly(2026, 3, 1), stat.PeriodMonth);
        Assert.Equal(LocationKind.Fridge, stat.LocationKind);
        Assert.Equal(RemovalReason.Discarded, stat.RemovalReason);
        Assert.Equal("Dairy", stat.CategoryLabel);
        Assert.False(stat.CategoryIsGlobal);
        Assert.Equal(1, stat.Count);
    }

    [Fact]
    public async Task Sweep_increments_an_existing_stat_row_for_matching_grain()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var retentionService = scope.ServiceProvider.GetRequiredService<RetentionService>();
        var (itemService, household, creator) = await SetUpAsync(scope, ct);
        var fridge = household.Locations.Single(l => l.Kind == LocationKind.Fridge);
        var removedAt = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero);

        var itemA = await itemService.CreateItemAsync(household.Id, fridge.Id, "Milk", DateOnly.FromDateTime(DateTime.UtcNow), creator.Id, ct: ct);
        itemA.RemovedAt = removedAt;
        itemA.RemovalReason = RemovalReason.Consumed;
        var itemB = await itemService.CreateItemAsync(household.Id, fridge.Id, "Milk2", DateOnly.FromDateTime(DateTime.UtcNow), creator.Id, ct: ct);
        itemB.RemovedAt = removedAt.AddDays(2);
        itemB.RemovalReason = RemovalReason.Consumed;
        await db.SaveChangesAsync(ct);

        var now = removedAt.AddDays(200);
        var swept = await retentionService.SweepExpiredDispositionsAsync(now, ct);

        Assert.Equal(2, swept);
        var stat = await db.ConsumptionStats.SingleAsync(s => s.HouseholdId == household.Id, ct);
        Assert.Equal(2, stat.Count);
    }

    [Fact]
    public async Task Sweep_uses_uncategorized_label_when_item_has_no_category()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var retentionService = scope.ServiceProvider.GetRequiredService<RetentionService>();
        var (itemService, household, creator) = await SetUpAsync(scope, ct);
        var fridge = household.Locations.Single(l => l.Kind == LocationKind.Fridge);
        var removedAt = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero);

        var item = await itemService.CreateItemAsync(household.Id, fridge.Id, "Mystery", DateOnly.FromDateTime(DateTime.UtcNow), creator.Id, ct: ct);
        item.RemovedAt = removedAt;
        item.RemovalReason = RemovalReason.Discarded;
        await db.SaveChangesAsync(ct);

        await retentionService.SweepExpiredDispositionsAsync(removedAt.AddDays(91), ct);

        var stat = await db.ConsumptionStats.SingleAsync(s => s.HouseholdId == household.Id, ct);
        Assert.Equal("Uncategorized", stat.CategoryLabel);
        Assert.False(stat.CategoryIsGlobal);
    }

    [Fact]
    public async Task Sweep_preserves_global_category_identity_in_the_durable_label()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var retentionService = scope.ServiceProvider.GetRequiredService<RetentionService>();
        var categoryService = scope.ServiceProvider.GetRequiredService<CategoryService>();
        await categoryService.SeedGlobalCategoriesAsync(ct);
        var (itemService, household, creator) = await SetUpAsync(scope, ct);
        var fridge = household.Locations.Single(l => l.Kind == LocationKind.Fridge);
        var dairy = await db.Categories.SingleAsync(c => c.HouseholdId == null && c.Name == "Dairy", ct);
        var removedAt = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero);

        var item = await itemService.CreateItemAsync(
            household.Id, fridge.Id, "Milk", DateOnly.FromDateTime(DateTime.UtcNow), creator.Id, categoryId: dairy.Id, ct: ct);
        item.RemovedAt = removedAt;
        item.RemovalReason = RemovalReason.Discarded;
        await db.SaveChangesAsync(ct);

        await retentionService.SweepExpiredDispositionsAsync(removedAt.AddDays(91), ct);

        var stat = await db.ConsumptionStats.SingleAsync(s => s.HouseholdId == household.Id, ct);
        Assert.Equal("Dairy", stat.CategoryLabel);
        Assert.True(stat.CategoryIsGlobal);
    }

    [Fact]
    public async Task Sweep_survives_a_removed_item_still_within_the_90_day_window()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var retentionService = scope.ServiceProvider.GetRequiredService<RetentionService>();
        var (itemService, household, creator) = await SetUpAsync(scope, ct);
        var fridge = household.Locations.Single(l => l.Kind == LocationKind.Fridge);
        var removedAt = DateTimeOffset.UtcNow.AddDays(-30);

        var item = await itemService.CreateItemAsync(household.Id, fridge.Id, "Recent", DateOnly.FromDateTime(DateTime.UtcNow), creator.Id, ct: ct);
        item.RemovedAt = removedAt;
        item.RemovalReason = RemovalReason.Consumed;
        await db.SaveChangesAsync(ct);

        var swept = await retentionService.SweepExpiredDispositionsAsync(DateTimeOffset.UtcNow, ct);

        Assert.Equal(0, swept);
        Assert.True(await db.Items.AnyAsync(i => i.Id == item.Id, ct));
        Assert.False(await db.ConsumptionStats.AnyAsync(s => s.HouseholdId == household.Id, ct));
    }

    [Fact]
    public async Task Sweep_survives_an_expired_but_not_removed_item_regardless_of_age()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var retentionService = scope.ServiceProvider.GetRequiredService<RetentionService>();
        var (itemService, household, creator) = await SetUpAsync(scope, ct);
        var fridge = household.Locations.Single(l => l.Kind == LocationKind.Fridge);

        var item = await itemService.CreateItemAsync(
            household.Id, fridge.Id, "Old expired", new DateOnly(2020, 1, 1), creator.Id, ct: ct);

        var swept = await retentionService.SweepExpiredDispositionsAsync(DateTimeOffset.UtcNow, ct);

        Assert.Equal(0, swept);
        Assert.True(await db.Items.AnyAsync(i => i.Id == item.Id, ct));
    }

    private static async Task<(ItemService ItemService, Household Household, User Creator)> SetUpAsync(
        IServiceScope scope, CancellationToken ct)
    {
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var householdService = scope.ServiceProvider.GetRequiredService<HouseholdService>();
        var itemService = scope.ServiceProvider.GetRequiredService<ItemService>();

        var uid = $"uid-retention-{Guid.NewGuid()}";
        var creator = await identityResolver.ResolveAsync(uid, $"{uid}@example.com", "Creator", ct);
        var household = await householdService.CreateHouseholdAsync(creator.Id, $"House {uid}", ct);

        return (itemService, household, creator);
    }
}
