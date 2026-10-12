using CourtFlow.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CourtFlow.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:CourtFlow", _postgres.GetConnectionString());
    }

    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourtFlowDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM tournament_teams;
            DELETE FROM teams;
            DELETE FROM clubs;
            DELETE FROM courts;
            DELETE FROM venues;
            DELETE FROM tournaments;
            DELETE FROM organizations WHERE "Id" <> '8f1c2a3e-5b6d-4e7f-9a0b-1c2d3e4f5a6b';
            """);
    }

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
