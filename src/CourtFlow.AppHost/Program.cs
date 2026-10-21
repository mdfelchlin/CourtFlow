using CourtFlow.AppHost;

PostgresDataVolume.Adopt();

var builder = DistributedApplication.CreateBuilder(args);

var userName = builder.AddParameter("courtflow-postgres-user");
var password = builder.AddParameter("courtflow-postgres-password");

// Kept in the AppHost's user secrets, never in appsettings.
var workOsApiKey = builder.AddParameter("workos-api-key", secret: true);
var resendApiKey = builder.AddParameter("resend-api-key", secret: true);

var postgres = builder.AddPostgres("postgres", userName, password)
    .WithImage("postgres", "16")
    .WithHostPort(5432)
    .WithDataVolume(PostgresDataVolume.Name);

var database = postgres.AddDatabase("CourtFlow", "courtflow");

// Local email goes to Mailpit's inbox rather than to real people.
var mailpit = builder.AddMailPit("mailpit");

builder.AddProject<Projects.CourtFlow_Api>("api", launchProfileName: null)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithHttpEndpoint(port: 5009, targetPort: 5009, name: "http", isProxied: false)
    .WithHttpsEndpoint(port: 7011, targetPort: 7011, name: "https", isProxied: false)
    .WithReference(database)
    .WaitFor(database)
    .WithReference(mailpit)
    .WithEnvironment("Email__Smtp", mailpit.Resource.UriExpression)
    .WithEnvironment("WorkOS__ApiKey", workOsApiKey)
    .WithEnvironment("Resend__ApiKey", resendApiKey);

builder.AddProject<Projects.CourtFlow_Web>("web", launchProfileName: null)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithHttpEndpoint(port: 5186, targetPort: 5186, name: "http", isProxied: false)
    .WithHttpsEndpoint(port: 7211, targetPort: 7211, name: "https", isProxied: false);

await builder.Build().RunAsync();

public partial class Program;