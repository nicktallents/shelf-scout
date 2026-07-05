using ShelfScout.Api.Domain;

namespace ShelfScout.Api.Tests;

public class ThresholdResolverTests
{
    [Fact]
    public void ResolveThreshold_uses_the_location_override_when_present()
    {
        var item = BuildItem(locationThresholdDays: 30, householdDefaultThresholdDays: 3);

        var threshold = ThresholdResolver.ResolveThreshold(item);

        Assert.Equal(30, threshold);
    }

    [Fact]
    public void ResolveThreshold_falls_back_to_the_household_default_when_location_has_no_override()
    {
        var item = BuildItem(locationThresholdDays: null, householdDefaultThresholdDays: 3);

        var threshold = ThresholdResolver.ResolveThreshold(item);

        Assert.Equal(3, threshold);
    }

    private static Item BuildItem(int? locationThresholdDays, int householdDefaultThresholdDays) => new()
    {
        Household = new Household { DefaultAlertThresholdDays = householdDefaultThresholdDays },
        Location = new Location { AlertThresholdDays = locationThresholdDays },
        Name = "Milk",
        ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow),
    };
}
