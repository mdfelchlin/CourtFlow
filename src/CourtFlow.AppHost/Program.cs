using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<CourtFlow_Api>("courtflow-api")
    .WithEnvironment("ConnectionStrings__Default", "Data Source=courtflow-dev.db");

builder.AddProject<CourtFlow_Web>("courtflow-web")
    .WithEnvironment("Api__BaseUrl", api.GetEndpoint("http"))
    .WithReference(api);

await builder.Build().RunAsync();
