namespace ShelfScout.Api.Tests;

public class StartupFailsClosedTests
{
    [Fact]
    public async Task Host_fails_to_start_when_database_is_unreachable()
    {
        await using var factory = new UnreachableDbApiFactory();

        var exception = Record.Exception(() => factory.Services);

        Assert.NotNull(exception);
    }
}
