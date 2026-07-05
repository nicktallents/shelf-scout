namespace ShelfScout.Api.Domain;

public class Identity
{
    public const string AuthentikProvider = "authentik";

    public int Id { get; set; }
    public string Provider { get; set; } = AuthentikProvider;
    public string Subject { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
