namespace ShelfScout.Api.Domain;

public class Household
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int DefaultAlertThresholdDays { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<Membership> Memberships { get; set; } = [];
    public List<Location> Locations { get; set; } = [];
    public List<Item> Items { get; set; } = [];
}
