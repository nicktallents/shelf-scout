namespace ShelfScout.Api.Domain;

public class Membership
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;

    public Role Role { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}
