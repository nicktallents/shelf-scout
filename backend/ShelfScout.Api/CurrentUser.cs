namespace ShelfScout.Api;

/// <summary>
/// The resolved identity for the current request: the durable <see cref="ShelfScout.Api.Domain.User"/>
/// (via <see cref="IdentityResolver"/>) plus the raw Authentik claims needed for authorization
/// decisions (ADR 0012).
/// </summary>
public sealed record CurrentUser(int UserId, string Uid, string? Email, string? DisplayName, IReadOnlyList<string> Groups)
{
    public const string DevelopmentFallbackUid = "dev-user";
    public const string DevelopmentFallbackEmail = "dev@localhost";
    public const string DevelopmentFallbackDisplayName = "Dev User";
}
