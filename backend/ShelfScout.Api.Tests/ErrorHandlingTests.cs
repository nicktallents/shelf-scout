using System.Net;

namespace ShelfScout.Api.Tests;

public class ErrorHandlingTests : IClassFixture<PostgresApiFactory>
{
    private readonly PostgresApiFactory _factory;

    public ErrorHandlingTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unhandled_exception_returns_problem_details_shape()
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/test/throw");
        request.Headers.Add("X-authentik-uid", "user-123");

        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var json = System.Text.Json.JsonDocument.Parse(body);
        Assert.True(json.RootElement.TryGetProperty("status", out var status));
        Assert.Equal(500, status.GetInt32());
        Assert.True(json.RootElement.TryGetProperty("title", out _));
    }
}
