using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ShelfScout.Api.Tests;

public class IdentityResolverTests : IClassFixture<PostgresApiFactory>
{
    private readonly PostgresApiFactory _factory;

    public IdentityResolverTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Resolve_provisions_a_new_user_on_first_sight()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();

        var user = await resolver.ResolveAsync("uid-new", "new@example.com", "New Person", ct);

        Assert.NotEqual(0, user.Id);
        Assert.Equal("new@example.com", user.Email);
        Assert.Equal("New Person", user.DisplayName);
    }

    [Fact]
    public async Task Resolve_returns_the_same_user_on_subsequent_resolves()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();

        var first = await resolver.ResolveAsync("uid-repeat", "alice@example.com", "Alice", ct);
        var second = await resolver.ResolveAsync("uid-repeat", "alice@example.com", "Alice", ct);

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task Resolve_matches_on_uid_so_an_email_change_never_duplicates_or_locks_out()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();

        var first = await resolver.ResolveAsync("uid-changes-email", "old@example.com", "Bob", ct);
        var second = await resolver.ResolveAsync("uid-changes-email", "new@example.com", "Bob", ct);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("new@example.com", second.Email);

        var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
        var identityCount = await db.Identities.CountAsync(i => i.Subject == "uid-changes-email", ct);
        Assert.Equal(1, identityCount);
    }
}
