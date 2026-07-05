using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShelfScout.Api.Domain;

namespace ShelfScout.Api.Tests;

public class HouseholdServiceTests : IClassFixture<PostgresApiFactory>
{
    private readonly PostgresApiFactory _factory;

    public HouseholdServiceTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateHousehold_seeds_fridge_freezer_pantry_with_correct_kinds_and_thresholds()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var householdService = scope.ServiceProvider.GetRequiredService<HouseholdService>();
        var creator = await identityResolver.ResolveAsync("uid-household-creator", "creator@example.com", "Creator", ct);

        var household = await householdService.CreateHouseholdAsync(creator.Id, "Casa del Creator", ct);

        var locations = await db.Locations
            .Where(l => l.HouseholdId == household.Id)
            .ToListAsync(ct);

        Assert.Equal(3, locations.Count);
        Assert.Contains(locations, l => l.Name == "Fridge" && l.Kind == LocationKind.Fridge && l.AlertThresholdDays == 3);
        Assert.Contains(locations, l => l.Name == "Freezer" && l.Kind == LocationKind.Freezer && l.AlertThresholdDays == 30);
        Assert.Contains(locations, l => l.Name == "Pantry" && l.Kind == LocationKind.Pantry && l.AlertThresholdDays == 14);
    }

    [Fact]
    public async Task CreateHousehold_binds_the_creator_as_owner()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var householdService = scope.ServiceProvider.GetRequiredService<HouseholdService>();
        var creator = await identityResolver.ResolveAsync("uid-owner-binding", "owner@example.com", "Owner", ct);

        var household = await householdService.CreateHouseholdAsync(creator.Id, "Owner's House", ct);

        var membership = await db.Memberships.SingleAsync(
            m => m.HouseholdId == household.Id && m.UserId == creator.Id, ct);
        Assert.Equal(Role.Owner, membership.Role);
    }

    [Fact]
    public async Task CreateHousehold_uses_the_seed_config_default_alert_threshold()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var householdService = scope.ServiceProvider.GetRequiredService<HouseholdService>();
        var creator = await identityResolver.ResolveAsync("uid-threshold-default", "t@example.com", "T", ct);

        var household = await householdService.CreateHouseholdAsync(creator.Id, "Default Threshold House", ct);

        Assert.Equal(SeedConfig.DefaultHouseholdAlertThresholdDays, household.DefaultAlertThresholdDays);
    }

    [Fact]
    public async Task A_user_with_no_household_is_a_valid_queryable_state()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var user = await identityResolver.ResolveAsync("uid-householdless", "lonely@example.com", "Lonely", ct);

        var membershipCount = await db.Memberships.CountAsync(m => m.UserId == user.Id, ct);

        Assert.Equal(0, membershipCount);
    }
}
