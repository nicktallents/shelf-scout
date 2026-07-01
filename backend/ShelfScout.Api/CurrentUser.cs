namespace ShelfScout.Api;

public sealed record CurrentUser(string Uid, string? Email, IReadOnlyList<string> Groups)
{
    public static readonly CurrentUser DevelopmentFallback = new("dev-user", "dev@localhost", []);
}
