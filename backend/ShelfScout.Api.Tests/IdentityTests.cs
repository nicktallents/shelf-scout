using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace ShelfScout.Api.Tests;

public class IdentityTests : IClassFixture<PostgresApiFactory>
{
    private readonly PostgresApiFactory _factory;

    public IdentityTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Context_reflects_the_identity_from_headers()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateAuthenticatedClient(
            "user-context-1", "alice@example.com", "Alice", "family", "admins");

        var response = await client.GetAsync("/api/context", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var user = json.RootElement.GetProperty("user");
        Assert.True(user.GetProperty("id").GetInt32() > 0);
        Assert.Equal("Alice", user.GetProperty("displayName").GetString());
        Assert.Equal("alice@example.com", user.GetProperty("email").GetString());
        Assert.Empty(json.RootElement.GetProperty("memberships").EnumerateArray());
        Assert.True(json.RootElement.GetProperty("canCreateHousehold").GetBoolean());
        Assert.Equal(
            "https://authentik.example.com/if/session-end/",
            json.RootElement.GetProperty("signOutUrl").GetString());
    }

    [Fact]
    public async Task CanCreateHousehold_is_false_without_the_family_group()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateAuthenticatedClient("user-no-family", groups: ["media-users"]);

        var response = await client.GetAsync("/api/context", ct);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.False(json.RootElement.GetProperty("canCreateHousehold").GetBoolean());
    }

    [Fact]
    public async Task Context_without_identity_header_fails_closed()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/api/context", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Any_api_route_without_identity_header_fails_closed()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/api/ping", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Non_api_routes_do_not_require_identity()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/health", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Same_uid_reuses_the_same_user_across_requests()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateAuthenticatedClient("user-repeat", "alice@example.com", "Alice");

        var first = await client.GetAsync("/api/context", ct);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync(ct));
        var firstId = firstJson.RootElement.GetProperty("user").GetProperty("id").GetInt32();

        var second = await client.GetAsync("/api/context", ct);
        using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync(ct));
        var secondId = secondJson.RootElement.GetProperty("user").GetProperty("id").GetInt32();

        Assert.Equal(firstId, secondId);
    }

    [Fact]
    public async Task Different_uids_provision_different_users()
    {
        var ct = TestContext.Current.CancellationToken;

        var firstResponse = await _factory.CreateAuthenticatedClient("user-a").GetAsync("/api/context", ct);
        using var firstJson = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync(ct));

        var secondResponse = await _factory.CreateAuthenticatedClient("user-b").GetAsync("/api/context", ct);
        using var secondJson = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync(ct));

        var firstId = firstJson.RootElement.GetProperty("user").GetProperty("id").GetInt32();
        var secondId = secondJson.RootElement.GetProperty("user").GetProperty("id").GetInt32();
        Assert.NotEqual(firstId, secondId);
    }

    [Fact]
    public async Task A_changed_email_updates_the_cached_profile_without_creating_a_new_user()
    {
        var ct = TestContext.Current.CancellationToken;

        var first = await _factory
            .CreateAuthenticatedClient("user-email-change", "old@example.com", "Bob")
            .GetAsync("/api/context", ct);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync(ct));
        var firstId = firstJson.RootElement.GetProperty("user").GetProperty("id").GetInt32();

        var second = await _factory
            .CreateAuthenticatedClient("user-email-change", "new@example.com", "Bob")
            .GetAsync("/api/context", ct);
        using var secondJson = JsonDocument.Parse(await second.Content.ReadAsStringAsync(ct));
        var secondUser = secondJson.RootElement.GetProperty("user");

        Assert.Equal(firstId, secondUser.GetProperty("id").GetInt32());
        Assert.Equal("new@example.com", secondUser.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Memberships_reflect_the_users_households_and_roles()
    {
        var ct = TestContext.Current.CancellationToken;

        using (var scope = _factory.Services.CreateScope())
        {
            var resolver = scope.ServiceProvider.GetRequiredService<IdentityResolver>();
            var households = scope.ServiceProvider.GetRequiredService<HouseholdService>();
            var user = await resolver.ResolveAsync("user-with-household", "carol@example.com", "Carol", ct);
            await households.CreateHouseholdAsync(user.Id, "Carol's Kitchen", ct);
        }

        var response = await _factory
            .CreateAuthenticatedClient("user-with-household", "carol@example.com", "Carol")
            .GetAsync("/api/context", ct);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var memberships = json.RootElement.GetProperty("memberships").EnumerateArray().ToArray();
        Assert.Single(memberships);
        Assert.Equal("Carol's Kitchen", memberships[0].GetProperty("householdName").GetString());
        Assert.Equal("Owner", memberships[0].GetProperty("role").GetString());
    }
}
