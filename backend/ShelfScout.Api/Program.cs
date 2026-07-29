using System.Text.Json;
using System.Text.Json.Serialization;
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

builder.Services.AddScoped<IdentityResolver>();
builder.Services.AddScoped<HouseholdService>();
builder.Services.AddScoped<ItemService>();
builder.Services.AddScoped<LocationService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<RetentionService>();
builder.Services.AddScoped<DevDataSeeder>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ShelfScoutDbContext>();

builder.Services.AddProblemDetails();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShelfScoutDbContext>();
    await db.Database.MigrateAsync();

    var categoryService = scope.ServiceProvider.GetRequiredService<CategoryService>();
    await categoryService.SeedGlobalCategoriesAsync();

    if (app.Environment.IsDevelopment())
    {
        var devDataSeeder = scope.ServiceProvider.GetRequiredService<DevDataSeeder>();
        await devDataSeeder.SeedAsync(DateTimeOffset.UtcNow);
    }
}

const string FamilyGroup = "family";

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
    string? email;
    string? displayName;
    IReadOnlyList<string> groups;

    if (string.IsNullOrEmpty(uid))
    {
        if (!app.Environment.IsDevelopment())
        {
            await WriteUnauthenticatedProblem(context);
            return;
        }

        // Lets `dotnet run` be debugged without Caddy/Authentik in front. Gated on
        // IsDevelopment() only — the fail-closed branch above still runs unchanged
        // in Testing and Production.
        uid = CurrentUser.DevelopmentFallbackUid;
        email = CurrentUser.DevelopmentFallbackEmail;
        displayName = CurrentUser.DevelopmentFallbackDisplayName;
        groups = [];
    }
    else
    {
        var emailHeader = context.Request.Headers["X-authentik-email"].ToString();
        var usernameHeader = context.Request.Headers["X-authentik-username"].ToString();
        var groupsHeader = context.Request.Headers["X-authentik-groups"].ToString();

        email = string.IsNullOrEmpty(emailHeader) ? null : emailHeader;
        displayName = string.IsNullOrEmpty(usernameHeader) ? null : usernameHeader;
        groups = string.IsNullOrEmpty(groupsHeader)
            ? []
            : groupsHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    var identityResolver = context.RequestServices.GetRequiredService<IdentityResolver>();
    var user = await identityResolver.ResolveAsync(uid, email, displayName, context.RequestAborted);

    context.SetCurrentUser(new CurrentUser(user.Id, uid, user.Email, user.DisplayName, groups));

    await next(context);
});

app.MapGet("/api/ping", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/context", async (HttpContext context, ShelfScoutDbContext db, IConfiguration configuration) =>
{
    var current = context.GetCurrentUser();

    var memberships = await db.Memberships
        .Where(m => m.UserId == current.UserId)
        .OrderBy(m => m.JoinedAt)
        .Select(m => new
        {
            householdId = m.HouseholdId,
            householdName = m.Household.Name,
            role = m.Role,
        })
        .ToListAsync(context.RequestAborted);

    return Results.Ok(new
    {
        user = new
        {
            id = current.UserId,
            displayName = current.DisplayName,
            email = current.Email,
        },
        memberships,
        canCreateHousehold = current.Groups.Contains(FamilyGroup),
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
