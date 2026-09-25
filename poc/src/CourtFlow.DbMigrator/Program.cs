using CourtFlow.Application;
using CourtFlow.Application.Abstractions;
using CourtFlow.Infrastructure;
using CourtFlow.Infrastructure.MultiTenancy;
using CourtFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var host = Host.CreateDefaultBuilder(args)
    .UseSerilog((ctx, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console())
    .ConfigureServices((ctx, services) =>
    {
        services.AddCourtFlowApplication();
        services.AddCourtFlowInfrastructure(ctx.Configuration);
        services.AddScoped<ITenantContext, NullTenantContext>();
    })
    .Build();

using var scope = host.Services.CreateScope();
await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

await host.StopAsync(TimeSpan.Zero);
