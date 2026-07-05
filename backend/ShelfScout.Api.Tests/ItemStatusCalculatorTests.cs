using ShelfScout.Api.Domain;

namespace ShelfScout.Api.Tests;

public class ItemStatusCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 7, 5);
    private const int Threshold = 3;

    [Fact]
    public void Expiry_before_today_is_expired()
    {
        var item = BuildItem(expiryDate: Today.AddDays(-1));

        var status = ItemStatusCalculator.ComputeStatus(item, Today, Threshold);

        Assert.Equal(ItemStatus.Expired, status);
    }

    [Fact]
    public void Expiry_equal_to_today_is_expiring_soon()
    {
        var item = BuildItem(expiryDate: Today);

        var status = ItemStatusCalculator.ComputeStatus(item, Today, Threshold);

        Assert.Equal(ItemStatus.ExpiringSoon, status);
    }

    [Fact]
    public void Expiry_at_today_plus_threshold_is_expiring_soon_inclusive()
    {
        var item = BuildItem(expiryDate: Today.AddDays(Threshold));

        var status = ItemStatusCalculator.ComputeStatus(item, Today, Threshold);

        Assert.Equal(ItemStatus.ExpiringSoon, status);
    }

    [Fact]
    public void Expiry_one_day_past_the_threshold_is_fresh()
    {
        var item = BuildItem(expiryDate: Today.AddDays(Threshold + 1));

        var status = ItemStatusCalculator.ComputeStatus(item, Today, Threshold);

        Assert.Equal(ItemStatus.Fresh, status);
    }

    [Fact]
    public void Removed_consumed_takes_precedence_over_an_expired_date()
    {
        var item = BuildItem(expiryDate: Today.AddDays(-10));
        item.RemovedAt = DateTimeOffset.UtcNow;
        item.RemovalReason = RemovalReason.Consumed;

        var status = ItemStatusCalculator.ComputeStatus(item, Today, Threshold);

        Assert.Equal(ItemStatus.Consumed, status);
    }

    [Fact]
    public void Removed_discarded_takes_precedence_over_an_expiring_soon_date()
    {
        var item = BuildItem(expiryDate: Today);
        item.RemovedAt = DateTimeOffset.UtcNow;
        item.RemovalReason = RemovalReason.Discarded;

        var status = ItemStatusCalculator.ComputeStatus(item, Today, Threshold);

        Assert.Equal(ItemStatus.Discarded, status);
    }

    private static Item BuildItem(DateOnly expiryDate) => new()
    {
        Name = "Milk",
        ExpiryDate = expiryDate,
    };
}
