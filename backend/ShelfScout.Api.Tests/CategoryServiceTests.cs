using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShelfScout.Api.Domain;

namespace ShelfScout.Api.Tests;

public class CategoryServiceTests : IClassFixture<PostgresApiFactory>
{
    private readonly PostgresApiFactory _factory;

    public CategoryServiceTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SeedGlobalCategories_seeds_every_configured_global_category_including_leftovers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var categoryService = scope.ServiceProvider.GetRequiredService<CategoryService>();

        await categoryService.SeedGlobalCategoriesAsync(ct);

        var globalNames = await db.Categories
            .Where(c => c.HouseholdId == null)
            .Select(c => c.Name)
            .ToListAsync(ct);

        foreach (var expected in SeedConfig.GlobalCategoryNames)
        {
            Assert.Contains(expected, globalNames);
        }

        Assert.Contains(SeedConfig.LeftoversCategoryName, globalNames);
    }

    [Fact]
    public async Task SeedGlobalCategories_is_idempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var categoryService = scope.ServiceProvider.GetRequiredService<CategoryService>();

        await categoryService.SeedGlobalCategoriesAsync(ct);
        await categoryService.SeedGlobalCategoriesAsync(ct);

        var count = await db.Categories.CountAsync(c => c.HouseholdId == null, ct);
        Assert.Equal(SeedConfig.GlobalCategoryNames.Count, count);
    }

    [Fact]
    public async Task CreateCustomCategory_scopes_it_to_the_household()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var (categoryService, household, _) = await SetUpAsync(scope, ct);

        var category = await categoryService.CreateCustomCategoryAsync(household.Id, "Leftover Curry", ct);

        Assert.Equal(household.Id, category.HouseholdId);
        Assert.False(category.IsGlobal);
    }

    [Fact]
    public async Task GetCategoriesForHousehold_returns_global_and_own_custom_categories_only()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var (categoryService, householdA, _) = await SetUpAsync(scope, ct);
        var (_, householdB, _) = await SetUpAsync(scope, ct);
        await categoryService.SeedGlobalCategoriesAsync(ct);

        var customA = await categoryService.CreateCustomCategoryAsync(householdA.Id, "A's Custom", ct);
        var customB = await categoryService.CreateCustomCategoryAsync(householdB.Id, "B's Custom", ct);

        var categoriesForA = await categoryService.GetCategoriesForHouseholdAsync(householdA.Id, ct);
        var names = categoriesForA.Select(c => c.Name).ToList();

        Assert.Contains(SeedConfig.LeftoversCategoryName, names);
        Assert.Contains(customA.Name, names);
        Assert.DoesNotContain(customB.Name, names);
    }

    [Fact]
    public async Task DeleteCustomCategory_nulls_referencing_items_category_id()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var itemService = scope.ServiceProvider.GetRequiredService<ItemService>();
        var (categoryService, household, creator) = await SetUpAsync(scope, ct);

        var category = await categoryService.CreateCustomCategoryAsync(household.Id, "Snacks", ct);
        var item = await itemService.CreateItemAsync(
            household.Id, household.Locations[0].Id, "Chips", DateOnly.FromDateTime(DateTime.UtcNow),
            creator.Id, categoryId: category.Id, ct: ct);

        await categoryService.DeleteCustomCategoryAsync(household.Id, category.Id, ct);

        var reloaded = await db.Items.SingleAsync(i => i.Id == item.Id, ct);
        Assert.Null(reloaded.CategoryId);
    }

    [Fact]
    public async Task DeleteCustomCategory_rejects_a_global_category()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var categoryService = scope.ServiceProvider.GetRequiredService<CategoryService>();
        var (_, household, _) = await SetUpAsync(scope, ct);
        await categoryService.SeedGlobalCategoriesAsync(ct);

        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var global = await db.Categories.FirstAsync(c => c.HouseholdId == null, ct);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => categoryService.DeleteCustomCategoryAsync(household.Id, global.Id, ct));
    }

    [Fact]
    public async Task DeleteCustomCategory_rejects_a_category_from_another_household()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var (categoryService, householdA, _) = await SetUpAsync(scope, ct);
        var (_, householdB, _) = await SetUpAsync(scope, ct);

        var categoryB = await categoryService.CreateCustomCategoryAsync(householdB.Id, "B's Category", ct);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => categoryService.DeleteCustomCategoryAsync(householdA.Id, categoryB.Id, ct));
    }

    private static async Task<(CategoryService CategoryService, Household Household, User Creator)> SetUpAsync(
        IServiceScope scope, CancellationToken ct)
    {
        var identityResolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
        var householdService = scope.ServiceProvider.GetRequiredService<HouseholdService>();
        var categoryService = scope.ServiceProvider.GetRequiredService<CategoryService>();

        var uid = $"uid-category-{Guid.NewGuid()}";
        var creator = await identityResolver.ResolveAsync(uid, $"{uid}@example.com", "Creator", ct);
        var household = await householdService.CreateHouseholdAsync(creator.Id, $"House {uid}", ct);

        return (categoryService, household, creator);
    }
}
