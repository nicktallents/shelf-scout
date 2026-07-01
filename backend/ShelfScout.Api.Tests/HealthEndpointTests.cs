using System.Net;

namespace ShelfScout.Api.Tests;

public class HealthEndpointTests : IClassFixture<PostgresApiFactory>
{
    private readonly PostgresApiFactory _factory;

    public HealthEndpointTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_returns_healthy_when_database_is_reachable()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.GetAsync("/health", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("Healthy", body);
    }
}
