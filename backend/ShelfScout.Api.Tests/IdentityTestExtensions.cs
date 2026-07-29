using Microsoft.AspNetCore.Mvc.Testing;

namespace ShelfScout.Api.Tests;

/// <summary>
/// Reusable header-identity helper for the Testcontainers HTTP harness: impersonates any
/// person/group by setting the <c>X-authentik-*</c> headers the edge would inject (ADR 0012).
/// Every later ticket that needs an authenticated <see cref="HttpClient"/> should use this
/// instead of hand-building headers on each request.
/// </summary>
public static class IdentityTestExtensions
{
    public static HttpClient CreateAuthenticatedClient(
        this WebApplicationFactory<Program> factory,
        string uid,
        string? email = null,
        string? username = null,
        params string[] groups)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-authentik-uid", uid);

        if (!string.IsNullOrEmpty(email))
        {
            client.DefaultRequestHeaders.Add("X-authentik-email", email);
        }

        if (!string.IsNullOrEmpty(username))
        {
            client.DefaultRequestHeaders.Add("X-authentik-username", username);
        }

        if (groups.Length > 0)
        {
            client.DefaultRequestHeaders.Add("X-authentik-groups", string.Join(',', groups));
        }

        return client;
    }
}
