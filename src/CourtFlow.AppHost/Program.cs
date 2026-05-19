using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// All endpoints are pinned to fixed ports so every config file, CORS allow-list, and
// developer-facing URL stays the same across runs (Aspire would otherwise allocate
// dynamic ports for each project).
//
//   API:  http://localhost:5033   https://localhost:7028
//   Web:  http://localhost:5111   https://localhost:7247
//
// These match `Properties/launchSettings.json` for the corresponding projects so
// standalone `dotnet run` behaves identically to running under the AppHost.
var migrator = builder.AddProject<CourtFlow_DbMigrator>("courtflow-migrator")
    .WithEnvironment("ConnectionStrings__Default", "Data Source=courtflow-dev.db");

var api = builder.AddProject<CourtFlow_Api>("courtflow-api")
    .WithHttpEndpoint(port: 5033, name: "http")
    .WithHttpsEndpoint(port: 7028, name: "https")
    .WithEnvironment("ConnectionStrings__Default", "Data Source=courtflow-dev.db")
    .WithEnvironment("Database__Provider", "Sqlite")
    .WaitFor(migrator);

builder.AddProject<CourtFlow_Web>("courtflow-web")
    .WithHttpEndpoint(port: 5111, name: "http")
    .WithHttpsEndpoint(port: 7247, name: "https")
    .WithEnvironment("Api__BaseUrl", api.GetEndpoint("https"))
    .WithReference(api);

await builder.Build().RunAsync();
