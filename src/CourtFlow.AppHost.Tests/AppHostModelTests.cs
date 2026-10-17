using System.Reflection;
using System.Runtime.CompilerServices;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using CourtFlow.AppHost;

namespace CourtFlow.AppHost.Tests;

public static class SkipVolumeAdoption
{
    [ModuleInitializer]
    public static void Initialize() =>
        Environment.SetEnvironmentVariable(PostgresDataVolume.SkipEnvironmentVariable, "1");
}

public class AppHostModelTests : IAsyncLifetime
{
    IDistributedApplicationTestingBuilder _appHost = null!;

    public async ValueTask InitializeAsync() =>
        _appHost = await DistributedApplicationTestingBuilder.CreateAsync<Program>();

    public async ValueTask DisposeAsync() => await _appHost.DisposeAsync();

    [Fact]
    public async Task Host_starts_the_api_blazor_app_postgres_and_mailpit()
    {
        var projects = _appHost.Resources.Where(IsProject).ToList();
        var servers = _appHost.Resources.OfType<PostgresServerResource>().ToList();
        var databases = _appHost.Resources.OfType<PostgresDatabaseResource>().ToList();
        var mailpit = Assert.Single(_appHost.Resources.OfType<MailPitContainerResource>());

        Assert.Equal(2, projects.Count);
        Assert.Contains(projects, IsApi);
        Assert.Contains(projects, IsWeb);
        var server = Assert.Single(servers);
        var database = Assert.Single(databases);
        Assert.Equal("postgres", server.Name);
        Assert.Equal("CourtFlow", database.Name);
        Assert.Equal("courtflow", database.DatabaseName);
        Assert.DoesNotContain(_appHost.Resources, resource =>
            resource.Name.Contains("migrator", StringComparison.OrdinalIgnoreCase) ||
            ProjectPath(resource)?.Contains(".Tests", StringComparison.OrdinalIgnoreCase) == true);

        Assert.Equal("courtflow", await server.UserNameParameter!.GetValueAsync(CancellationToken.None));
        Assert.Equal("courtflow", await server.PasswordParameter.GetValueAsync(CancellationToken.None));
        Assert.Equal("16", ImageTag(server));
        Assert.Equal(5432, Endpoint(server, "tcp").Port);
        var volume = DataVolume(server);
        Assert.Equal(PostgresDataVolume.Name, volume.Source);
        Assert.Equal("/var/lib/postgresql/data", volume.Target);

        var api = projects.Single(IsApi);
        Assert.Equal(5009, Endpoint(api, "http").Port);
        Assert.Equal(7011, Endpoint(api, "https").Port);
        Assert.False(Endpoint(api, "http").IsProxied);
        Assert.False(Endpoint(api, "https").IsProxied);
        Assert.Contains(api.Annotations.OfType<WaitAnnotation>(), wait => wait.Resource.Name == "CourtFlow");
        Assert.Contains(api.Annotations.OfType<ResourceRelationshipAnnotation>(), relationship => relationship.Resource.Name == "CourtFlow");
        Assert.Contains(api.Annotations.OfType<ResourceRelationshipAnnotation>(), relationship => relationship.Resource == mailpit);
#pragma warning disable CS0618
        var apiEnvironment = await ((IResourceWithEnvironment)api).GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish);
#pragma warning restore CS0618
        Assert.Contains("Email__Smtp", apiEnvironment.Keys);
        Assert.Contains("WorkOS__ApiKey", apiEnvironment.Keys);
        Assert.Contains("Resend__ApiKey", apiEnvironment.Keys);
        var apiKeys = _appHost.Resources.OfType<ParameterResource>()
            .Where(parameter => parameter.Name is "workos-api-key" or "resend-api-key")
            .ToList();
        Assert.Equal(2, apiKeys.Count);
        Assert.All(apiKeys, parameter => Assert.True(parameter.Secret));

        var web = projects.Single(IsWeb);
        Assert.Equal(5186, Endpoint(web, "http").Port);
        Assert.Equal(7211, Endpoint(web, "https").Port);
        Assert.False(Endpoint(web, "http").IsProxied);
        Assert.False(Endpoint(web, "https").IsProxied);
        Assert.DoesNotContain(web.Annotations.OfType<WaitAnnotation>(), wait => wait.Resource.Name == "api");
        Assert.DoesNotContain(EnvironmentNames(web), name => name.Contains("ApiBaseUrl", StringComparison.OrdinalIgnoreCase));
    }

    static bool IsProject(IResource resource) => ProjectPath(resource) is not null;

    static bool IsApi(IResource resource) =>
        ProjectPath(resource)?.EndsWith("CourtFlow.Api.csproj", StringComparison.OrdinalIgnoreCase) == true;

    static bool IsWeb(IResource resource) =>
        ProjectPath(resource)?.EndsWith("CourtFlow.Web.csproj", StringComparison.OrdinalIgnoreCase) == true;

    static string? ProjectPath(IResource resource) =>
        resource.Annotations.OfType<IProjectMetadata>().SingleOrDefault()?.ProjectPath;

    static EndpointAnnotation Endpoint(IResource resource, string name) =>
        Assert.Single(resource.Annotations.OfType<EndpointAnnotation>(), endpoint => endpoint.Name == name);

    static ContainerMountAnnotation DataVolume(IResource resource) =>
        Assert.Single(resource.Annotations.OfType<ContainerMountAnnotation>(), mount => mount.Type == ContainerMountType.Volume);

    static string? ImageTag(IResource resource)
    {
        Assert.True(resource.TryGetLastAnnotation<ContainerImageAnnotation>(out var image));
        return image!.Tag;
    }

    static IEnumerable<string> EnvironmentNames(IResource resource)
    {
        var nameField = typeof(EnvironmentCallbackAnnotation).GetField("_name", BindingFlags.Instance | BindingFlags.NonPublic);
        return resource.Annotations.OfType<EnvironmentCallbackAnnotation>()
            .Select(annotation => nameField?.GetValue(annotation) as string)
            .OfType<string>();
    }
}
