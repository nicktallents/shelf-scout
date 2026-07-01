using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ShelfScout.Api.Tests;

public sealed class UnreachableDbApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:ShelfScout",
            "Host=127.0.0.1;Port=1;Timeout=2;Database=unreachable;Username=x;Password=x");
    }
}
