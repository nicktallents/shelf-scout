using System.Net;
using System.Text.Json;

namespace ShelfScout.Api.Tests;

public class IdentityTests : IClassFixture<PostgresApiFactory>
{
    private readonly PostgresApiFactory _factory;

    public IdentityTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Whoami_reflects_the_identity_from_headers()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/whoami");
        request.Headers.Add("X-authentik-uid", "user-123");
        request.Headers.Add("X-authentik-email", "alice@example.com");
        request.Headers.Add("X-authentik-groups", "family,admins");

        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.Equal("user-123", json.RootElement.GetProperty("uid").GetString());
        Assert.Equal("alice@example.com", json.RootElement.GetProperty("email").GetString());
        var groups = json.RootElement.GetProperty("groups").EnumerateArray().Select(g => g.GetString()!).ToArray();
        Assert.Equal(["family", "admins"], groups);
        Assert.Equal(
            "https://authentik.example.com/if/session-end/",
            json.RootElement.GetProperty("signOutUrl").GetString());
    }

    [Fact]
    public async Task Whoami_without_identity_header_fails_closed()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/api/whoami", ct);

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
    public async Task Identity_resolution_is_stateless_per_request()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        using var first = new HttpRequestMessage(HttpMethod.Get, "/api/whoami");
        first.Headers.Add("X-authentik-uid", "user-a");
        var firstResponse = await client.SendAsync(first, ct);
        using var firstJson = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync(ct));

        using var second = new HttpRequestMessage(HttpMethod.Get, "/api/whoami");
        second.Headers.Add("X-authentik-uid", "user-b");
        var secondResponse = await client.SendAsync(second, ct);
        using var secondJson = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync(ct));

        Assert.Equal("user-a", firstJson.RootElement.GetProperty("uid").GetString());
        Assert.Equal("user-b", secondJson.RootElement.GetProperty("uid").GetString());
    }
}
