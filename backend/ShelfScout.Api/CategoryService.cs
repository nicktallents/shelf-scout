using Microsoft.EntityFrameworkCore;
using ShelfScout.Api.Domain;

namespace ShelfScout.Api;

/// <summary>
/// Global (household_id NULL, system-seeded, household-immutable) and custom (per-household)
/// Category management (ADR 0004, CONTEXT.md).
/// </summary>
public class CategoryService(ShelfScoutDbContext db)
{
    /// <summary>
    /// Idempotently inserts any global Category from <see cref="SeedConfig.GlobalCategoryNames"/>
    /// that doesn't already exist. Safe to call on every startup.
    /// </summary>
    public async Task SeedGlobalCategoriesAsync(CancellationToken ct = default)
    {
        var existing = await db.Categories
            .Where(c => c.HouseholdId == null)
            .Select(c => c.Name)
            .ToListAsync(ct);

        var missing = SeedConfig.GlobalCategoryNames.Except(existing);

        foreach (var name in missing)
        {
            db.Categories.Add(new Category { HouseholdId = null, Name = name });
        }

        await db.SaveChangesAsync(ct);
    }

    public Task<List<Category>> GetCategoriesForHouseholdAsync(int householdId, CancellationToken ct = default) =>
        db.Categories
            .Where(c => c.HouseholdId == null || c.HouseholdId == householdId)
            .ToListAsync(ct);

    public async Task<Category> CreateCustomCategoryAsync(int householdId, string name, CancellationToken ct = default)
    {
        var category = new Category { HouseholdId = householdId, Name = name };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return category;
    }

    public async Task DeleteCustomCategoryAsync(int householdId, int categoryId, CancellationToken ct = default)
    {
        var category = await db.Categories.SingleOrDefaultAsync(
            c => c.Id == categoryId && c.HouseholdId == householdId, ct);

        if (category is null)
        {
            throw new InvalidOperationException(
                $"Category {categoryId} is not a custom category belonging to household {householdId}.");
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
    }
}
