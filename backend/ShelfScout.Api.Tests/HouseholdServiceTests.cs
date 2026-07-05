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

    [Fact]
    public async Task AddMember_lets_an_owner_invite_a_new_member()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var householdService = scope.ServiceProvider.GetRequiredService<HouseholdService>();
        var owner = await identityResolver.ResolveAsync($"uid-owner-{Guid.NewGuid()}", "o@example.com", "Owner", ct);
        var household = await householdService.CreateHouseholdAsync(owner.Id, "Add Member House", ct);
        var invitee = await identityResolver.ResolveAsync($"uid-invitee-{Guid.NewGuid()}", "i@example.com", "Invitee", ct);

        var membership = await householdService.AddMemberAsync(household.Id, owner.Id, invitee.Id, Role.Member, ct);

        Assert.Equal(Role.Member, membership.Role);
        var reloaded = await db.Memberships.SingleAsync(
            m => m.HouseholdId == household.Id && m.UserId == invitee.Id, ct);
        Assert.Equal(Role.Member, reloaded.Role);
    }

    [Fact]
    public async Task AddMember_rejects_a_user_who_is_already_a_member()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var householdService = scope.ServiceProvider.GetRequiredService<HouseholdService>();
        var owner = await identityResolver.ResolveAsync($"uid-owner-{Guid.NewGuid()}", "dup@example.com", "Owner", ct);
        var household = await householdService.CreateHouseholdAsync(owner.Id, "Dup Member House", ct);
        var invitee = await identityResolver.ResolveAsync($"uid-invitee-{Guid.NewGuid()}", "dupinv@example.com", "Invitee", ct);
        await householdService.AddMemberAsync(household.Id, owner.Id, invitee.Id, Role.Member, ct);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => householdService.AddMemberAsync(household.Id, owner.Id, invitee.Id, Role.Member, ct));
    }

    [Fact]
    public async Task AddMember_rejects_a_non_owner()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var householdService = scope.ServiceProvider.GetRequiredService<HouseholdService>();
        var owner = await identityResolver.ResolveAsync($"uid-owner-{Guid.NewGuid()}", "o2@example.com", "Owner", ct);
        var household = await householdService.CreateHouseholdAsync(owner.Id, "Gated House", ct);
        var member = await identityResolver.ResolveAsync($"uid-member-{Guid.NewGuid()}", "m@example.com", "Member", ct);
        await householdService.AddMemberAsync(household.Id, owner.Id, member.Id, Role.Member, ct);
        var outsider = await identityResolver.ResolveAsync($"uid-outsider-{Guid.NewGuid()}", "out@example.com", "Outsider", ct);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => householdService.AddMemberAsync(household.Id, member.Id, outsider.Id, Role.Member, ct));
    }

    [Fact]
    public async Task Households_may_have_multiple_owners()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var householdService = scope.ServiceProvider.GetRequiredService<HouseholdService>();
        var owner = await identityResolver.ResolveAsync($"uid-owner-{Guid.NewGuid()}", "o3@example.com", "Owner", ct);
        var household = await householdService.CreateHouseholdAsync(owner.Id, "Multi Owner House", ct);
        var coOwner = await identityResolver.ResolveAsync($"uid-coowner-{Guid.NewGuid()}", "co@example.com", "CoOwner", ct);

        await householdService.AddMemberAsync(household.Id, owner.Id, coOwner.Id, Role.Owner, ct);
        // The new owner can now itself gate membership operations.
        var third = await identityResolver.ResolveAsync($"uid-third-{Guid.NewGuid()}", "t2@example.com", "Third", ct);
        await householdService.AddMemberAsync(household.Id, coOwner.Id, third.Id, Role.Member, ct);

        var ownerCount = await db.Memberships.CountAsync(
            m => m.HouseholdId == household.Id && m.Role == Role.Owner, ct);
        Assert.Equal(2, ownerCount);
    }

    [Fact]
    public async Task RemoveMember_deletes_only_that_membership_leaving_the_user_and_other_memberships_intact()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var householdService = scope.ServiceProvider.GetRequiredService<HouseholdService>();
        var owner = await identityResolver.ResolveAsync($"uid-owner-{Guid.NewGuid()}", "o4@example.com", "Owner", ct);
        var householdA = await householdService.CreateHouseholdAsync(owner.Id, "House A", ct);
        var householdB = await householdService.CreateHouseholdAsync(owner.Id, "House B", ct);
        var member = await identityResolver.ResolveAsync($"uid-member-{Guid.NewGuid()}", "m2@example.com", "Member", ct);
        await householdService.AddMemberAsync(householdA.Id, owner.Id, member.Id, Role.Member, ct);
        await householdService.AddMemberAsync(householdB.Id, owner.Id, member.Id, Role.Member, ct);

        await householdService.RemoveMemberAsync(householdA.Id, owner.Id, member.Id, ct);

        var remainingMemberships = await db.Memberships.Where(m => m.UserId == member.Id).ToListAsync(ct);
        Assert.Single(remainingMemberships);
        Assert.Equal(householdB.Id, remainingMemberships[0].HouseholdId);
        Assert.True(await db.Users.AnyAsync(u => u.Id == member.Id, ct));
    }

    [Fact]
    public async Task RemoveMember_rejects_a_non_owner()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var householdService = scope.ServiceProvider.GetRequiredService<HouseholdService>();
        var owner = await identityResolver.ResolveAsync($"uid-owner-{Guid.NewGuid()}", "o5@example.com", "Owner", ct);
        var household = await householdService.CreateHouseholdAsync(owner.Id, "Gated Removal House", ct);
        var memberA = await identityResolver.ResolveAsync($"uid-member-{Guid.NewGuid()}", "ma@example.com", "MemberA", ct);
        var memberB = await identityResolver.ResolveAsync($"uid-member-{Guid.NewGuid()}", "mb@example.com", "MemberB", ct);
        await householdService.AddMemberAsync(household.Id, owner.Id, memberA.Id, Role.Member, ct);
        await householdService.AddMemberAsync(household.Id, owner.Id, memberB.Id, Role.Member, ct);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => householdService.RemoveMemberAsync(household.Id, memberA.Id, memberB.Id, ct));
    }

    [Fact]
    public async Task RenameHousehold_lets_an_owner_rename_but_rejects_a_non_owner()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var householdService = scope.ServiceProvider.GetRequiredService<HouseholdService>();
        var owner = await identityResolver.ResolveAsync($"uid-owner-{Guid.NewGuid()}", "o6@example.com", "Owner", ct);
        var household = await householdService.CreateHouseholdAsync(owner.Id, "Old Name", ct);
        var member = await identityResolver.ResolveAsync($"uid-member-{Guid.NewGuid()}", "m3@example.com", "Member", ct);
        await householdService.AddMemberAsync(household.Id, owner.Id, member.Id, Role.Member, ct);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => householdService.RenameHouseholdAsync(household.Id, member.Id, "Hijacked Name", ct));

        var renamed = await householdService.RenameHouseholdAsync(household.Id, owner.Id, "New Name", ct);
        Assert.Equal("New Name", renamed.Name);
    }

    [Fact]
    public async Task DeleteHousehold_lets_an_owner_cascade_delete_all_owned_data_including_consumption_stats()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var householdService = scope.ServiceProvider.GetRequiredService<HouseholdService>();
        var categoryService = scope.ServiceProvider.GetRequiredService<CategoryService>();
        var itemService = scope.ServiceProvider.GetRequiredService<ItemService>();
        var retentionService = scope.ServiceProvider.GetRequiredService<RetentionService>();
        var owner = await identityResolver.ResolveAsync($"uid-owner-{Guid.NewGuid()}", "o7@example.com", "Owner", ct);
        var household = await householdService.CreateHouseholdAsync(owner.Id, "Doomed House", ct);
        var custom = await categoryService.CreateCustomCategoryAsync(household.Id, "Doomed Category", ct);
        var fridge = household.Locations.Single(l => l.Kind == LocationKind.Fridge);
        var removedItem = await itemService.CreateItemAsync(
            household.Id, fridge.Id, "Doomed Item", DateOnly.FromDateTime(DateTime.UtcNow), owner.Id, categoryId: custom.Id, ct: ct);
        var removedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        removedItem.RemovedAt = removedAt;
        removedItem.RemovalReason = RemovalReason.Discarded;
        await db.SaveChangesAsync(ct);
        await retentionService.SweepExpiredDispositionsAsync(removedAt.AddDays(200), ct);
        Assert.True(await db.ConsumptionStats.AnyAsync(s => s.HouseholdId == household.Id, ct));

        await householdService.DeleteHouseholdAsync(household.Id, owner.Id, ct);

        Assert.False(await db.Households.AnyAsync(h => h.Id == household.Id, ct));
        Assert.False(await db.Memberships.AnyAsync(m => m.HouseholdId == household.Id, ct));
        Assert.False(await db.Locations.AnyAsync(l => l.HouseholdId == household.Id, ct));
        Assert.False(await db.Categories.AnyAsync(c => c.HouseholdId == household.Id, ct));
        Assert.False(await db.ConsumptionStats.AnyAsync(s => s.HouseholdId == household.Id, ct));
        Assert.True(await db.Users.AnyAsync(u => u.Id == owner.Id, ct));
    }

    [Fact]
    public async Task DeleteHousehold_rejects_a_non_owner()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var householdService = scope.ServiceProvider.GetRequiredService<HouseholdService>();
        var owner = await identityResolver.ResolveAsync($"uid-owner-{Guid.NewGuid()}", "o8@example.com", "Owner", ct);
        var household = await householdService.CreateHouseholdAsync(owner.Id, "Protected House", ct);
        var member = await identityResolver.ResolveAsync($"uid-member-{Guid.NewGuid()}", "m4@example.com", "Member", ct);
        await householdService.AddMemberAsync(household.Id, owner.Id, member.Id, Role.Member, ct);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => householdService.DeleteHouseholdAsync(household.Id, member.Id, ct));
    }
}
