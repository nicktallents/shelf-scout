namespace ShelfScout.Api.Domain;

public class User
{
    public int Id { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<Identity> Identities { get; set; } = [];
    public List<Membership> Memberships { get; set; } = [];
}
