namespace ShelfScout.Api.Domain;

public class Location
{
    public int Id { get; set; }

    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;

    public string Name { get; set; } = null!;
    public LocationKind? Kind { get; set; }
    public int? AlertThresholdDays { get; set; }

    public List<Item> Items { get; set; } = [];
}
