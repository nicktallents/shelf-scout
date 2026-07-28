using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var shelfScoutDb = postgres.AddDatabase("ShelfScout");

var api = builder.AddProject<Projects.ShelfScout_Api>("api")
    .WithReference(shelfScoutDb)
    .WaitFor(shelfScoutDb);

builder.AddViteApp("frontend", "../../frontend")
    .WithReference(api)
    .WithEnvironment("VITE_API_PROXY_TARGET", api.GetEndpoint("http"))
    .WaitFor(api);

builder.Build().Run();
