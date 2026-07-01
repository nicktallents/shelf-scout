using System.Net;

namespace ShelfScout.Api.Tests;

public class ApiRoutingTests : IClassFixture<PostgresApiFactory>
{
    private readonly PostgresApiFactory _factory;

    public ApiRoutingTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Sample_api_route_is_reachable()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/ping");
        request.Headers.Add("X-authentik-uid", "user-123");

        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_api_route_returns_404_problem_details()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/does-not-exist");
        request.Headers.Add("X-authentik-uid", "user-123");

        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Unknown_non_api_route_serves_spa_shell()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/some/client/side/route", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("<div id=\"app\">", body);
    }
}
