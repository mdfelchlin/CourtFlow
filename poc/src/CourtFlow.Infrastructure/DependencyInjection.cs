using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Brackets;
using CourtFlow.Application.CourtStatus;
using CourtFlow.Application.Scheduling;
using CourtFlow.Application.Standings;
using CourtFlow.Application.ReadModels.DivisionReadiness;
using CourtFlow.Application.ReadModels.PublicEventReadiness;
using CourtFlow.Application.ReadModels.DivisionValidation;
using CourtFlow.Application.ReadModels.TiebreakerChain;
using CourtFlow.Application.Reflow;
using CourtFlow.Application.StageMaterialization;
using CourtFlow.Application.Matches;
using CourtFlow.Infrastructure.Brackets;
using CourtFlow.Infrastructure.Reflow;
using CourtFlow.Infrastructure.CourtStatus;
using CourtFlow.Infrastructure.MultiTenancy;
using CourtFlow.Infrastructure.Options;
using CourtFlow.Infrastructure.Persistence;
using CourtFlow.Infrastructure.Persistence.Serialization;
using CourtFlow.Infrastructure.Scheduling;
using CourtFlow.Infrastructure.Standings;
using CourtFlow.Infrastructure.Observability;
using CourtFlow.Infrastructure.Matches;
using CourtFlow.Infrastructure.ReadModels;
using CourtFlow.Infrastructure.StageMaterialization;
using CourtFlow.Application.ReadModels.MatchClockDelaySignal;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Microsoft.Extensions.Hosting;

namespace CourtFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCourtFlowInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(config.GetSection(DatabaseOptions.SectionName))
            .PostConfigure(o =>
            {
                if (string.IsNullOrWhiteSpace(o.ConnectionString))
                    o.ConnectionString = config.GetConnectionString("Default") ?? "";
            })
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();
        services.AddOptions<CorsOptions>()
            .Bind(config.GetSection(CorsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<CorsOptions>, CorsOptionsValidator>();

        services.AddScoped<OutboxEventSerializer>();
        services.AddScoped<OutboxPersistenceInterceptor>();

        services.AddDbContext<AppDbContext>((sp, opts) =>
        {
            var o = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            if (o.Provider == DatabaseProvider.LibSql)
                opts.UseLibSql(o.ConnectionString);
            else
                opts.UseSqlite(o.ConnectionString);

            opts.UseSnakeCaseNamingConvention();
            opts.AddInterceptors(sp.GetRequiredService<OutboxPersistenceInterceptor>());
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();

        services.AddScoped<IScheduleGenerator, RoundRobinScheduleGenerator>();
        services.AddScoped<IConstraintChecker, ConstraintChecker>();
        services.AddScoped<IBracketBuilder, BracketBuilder>();
        services.AddScoped<IBracketProgressionService, BracketProgressionService>();
        services.AddScoped<IStandingsCalculator, StandingsCalculator>();
        services.AddScoped<ICourtStatusService, CourtStatusService>();
        services.AddScoped<IReflowPreviewEngine, ReflowPreviewEngine>();
        services.AddSingleton<IReflowApplyTokenIssuer, ReflowApplyTokenIssuer>();
        services.AddScoped<IPublicEventReadiness, PublicEventReadinessService>();
        services.AddScoped<IDivisionReadiness, DivisionReadinessService>();
        services.AddScoped<IDivisionValidation, DivisionValidationService>();
        services.AddScoped<IMatchProgressionService, MatchProgressionService>();
        services.AddSingleton<ITiebreakerChain, TiebreakerChainService>();
        services.AddSingleton<IDelayCalculation, DelayCalculationService>();
        services.AddSingleton<CourtFlowMetrics>();
        services.AddScoped<IStageMaterialization, StageMaterializationService>();

        services.AddOptions<ScheduleVersionsOptions>()
            .Bind(config.GetSection(ScheduleVersionsOptions.SectionName));
        services.AddSingleton<IScheduleApplyTokenIssuer, ScheduleApplyTokenIssuer>();

        return services;
    }

    /// <summary>Hosted poller + MediatR dispatch for transactional outbox (run in Api, not Migrator).</summary>
    public static IServiceCollection AddCourtFlowOutboxDispatcher(this IServiceCollection services)
    {
        services.AddHostedService<Outbox.OutboxDispatcherHostedService>();
        services.AddHostedService<HostedServices.TournamentAutoArchiveHostedService>();
        return services;
    }
}
