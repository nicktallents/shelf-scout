using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShelfScout.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

builder.Services.AddDbContext<ShelfScoutDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("ShelfScout"))
    .UseSnakeCaseNamingConvention());

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ShelfScoutDbContext>();

builder.Services.AddProblemDetails();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthCheckResponse,
});

app.MapGet("/api/ping", () => Results.Ok(new { status = "ok" }));

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapGet("/api/test/throw", IResult () => throw new InvalidOperationException("boom"));
}

app.MapFallback("/api/{**catch-all}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound));

app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

static Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    var payload = JsonSerializer.Serialize(new { status = report.Status.ToString() });
    return context.Response.WriteAsync(payload);
}

public partial class Program;
