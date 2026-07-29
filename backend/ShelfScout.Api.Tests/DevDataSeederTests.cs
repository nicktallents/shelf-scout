using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShelfScout.Api.Domain;

namespace ShelfScout.Api.Tests;

public class DevDataSeederTests : IClassFixture<DevelopmentApiFactory>
{
    private readonly DevelopmentApiFactory _factory;

    public DevDataSeederTests(DevelopmentApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Startup_seeds_a_household_for_the_development_fallback_user_with_items_across_report_buckets()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var itemService = scope.ServiceProvider.GetRequiredService<ItemService>();

        var user = await identityResolver.ResolveAsync(
            CurrentUser.DevelopmentFallbackUid,
            CurrentUser.DevelopmentFallbackEmail,
            CurrentUser.DevelopmentFallbackDisplayName,
            ct);

        var household = await db.Households
            .Include(h => h.Memberships)
            .SingleAsync(h => h.Name == DevDataSeeder.HouseholdName, ct);
        Assert.Contains(household.Memberships, m => m.UserId == user.Id);

        var items = await db.Items
            .Include(i => i.Location)
            .Include(i => i.Household)
            .Where(i => i.HouseholdId == household.Id)
            .ToListAsync(ct);
        Assert.NotEmpty(items);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var statuses = items
            .Select(i => ItemStatusCalculator.ComputeStatus(i, today, ThresholdResolver.ResolveThreshold(i)))
            .ToList();

        Assert.Contains(ItemStatus.Expired, statuses);
        Assert.Contains(ItemStatus.ExpiringSoon, statuses);

        var activeInventory = await itemService.GetActiveInventoryAsync(household.Id, ct);
        Assert.Equal(items.Count, activeInventory.Count);
    }

    [Fact]
    public async Task Seeding_twice_does_not_duplicate_the_fixture()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<DevDataSeeder>();

        // Startup already seeded once; seed again explicitly to prove idempotency.
        await seeder.SeedAsync(DateTimeOffset.UtcNow, ct);
        await seeder.SeedAsync(DateTimeOffset.UtcNow, ct);

        var householdCount = await db.Households.CountAsync(h => h.Name == DevDataSeeder.HouseholdName, ct);
        Assert.Equal(1, householdCount);

        var household = await db.Households.SingleAsync(h => h.Name == DevDataSeeder.HouseholdName, ct);
        var itemCount = await db.Items.CountAsync(i => i.HouseholdId == household.Id, ct);
        var firstRunItemCount = itemCount;

        await seeder.SeedAsync(DateTimeOffset.UtcNow, ct);

        var itemCountAfterAnotherSeed = await db.Items.CountAsync(i => i.HouseholdId == household.Id, ct);
        Assert.Equal(firstRunItemCount, itemCountAfterAnotherSeed);
    }
}

public class DevDataSeederGatingTests : IClassFixture<PostgresApiFactory>
{
    private readonly PostgresApiFactory _factory;

    public DevDataSeederGatingTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Sample_data_is_not_seeded_outside_development()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();

        var householdCount = await db.Households.CountAsync(h => h.Name == DevDataSeeder.HouseholdName, ct);

        Assert.Equal(0, householdCount);
    }
}
