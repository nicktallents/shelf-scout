using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
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

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api"))
    {
        await next(context);
        return;
    }

    var uid = context.Request.Headers["X-authentik-uid"].ToString();

    if (string.IsNullOrEmpty(uid))
    {
        if (app.Environment.IsDevelopment())
        {
            // Lets `dotnet run` be debugged without Caddy/Authentik in front. Gated on
            // IsDevelopment() only — the fail-closed branch below still runs unchanged
            // in Testing and Production.
            context.SetCurrentUser(CurrentUser.DevelopmentFallback);
            await next(context);
            return;
        }

        await WriteUnauthenticatedProblem(context);
        return;
    }

    var email = context.Request.Headers["X-authentik-email"].ToString();
    var groups = context.Request.Headers["X-authentik-groups"].ToString();

    context.SetCurrentUser(new CurrentUser(
        uid,
        string.IsNullOrEmpty(email) ? null : email,
        string.IsNullOrEmpty(groups)
            ? []
            : groups.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));

    await next(context);
});

app.MapGet("/api/ping", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/whoami", (HttpContext context, IConfiguration configuration) =>
{
    var user = context.GetCurrentUser();
    return Results.Ok(new
    {
        uid = user.Uid,
        email = user.Email,
        groups = user.Groups,
        signOutUrl = configuration["Authentik:SignOutUrl"],
    });
});

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

static async Task WriteUnauthenticatedProblem(HttpContext context)
{
    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    var problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();
    await problemDetailsService.WriteAsync(new ProblemDetailsContext
    {
        HttpContext = context,
        ProblemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Identity required",
            Detail = "Request is missing a valid identity header.",
        },
    });
}

public partial class Program;
