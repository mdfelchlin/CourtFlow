using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Brackets;
using CourtFlow.Application.CourtStatus;
using CourtFlow.Application.Scheduling;
using CourtFlow.Application.Standings;
using CourtFlow.Infrastructure.Brackets;
using CourtFlow.Infrastructure.CourtStatus;
using CourtFlow.Infrastructure.Identity;
using CourtFlow.Infrastructure.MultiTenancy;
using CourtFlow.Infrastructure.Persistence;
using CourtFlow.Infrastructure.Scheduling;
using CourtFlow.Infrastructure.Standings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CourtFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCourtFlowInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connection = ResolveConnectionString(config);

        services.AddDbContext<AppDbContext>(opts =>
        {
            if (IsLibSqlConnection(connection))
            {
                opts.UseLibSql(connection);
            }
            else
            {
                opts.UseSqlite(connection);
            }

            opts.UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = false;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.AllowedForNewUsers = true;
        })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IIdentityService, IdentityService>();

        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();

        services.AddScoped<IScheduleGenerator, RoundRobinScheduleGenerator>();
        services.AddScoped<IConstraintChecker, ConstraintChecker>();
        services.AddScoped<IBracketBuilder, BracketBuilder>();
        services.AddScoped<IBracketProgressionService, BracketProgressionService>();
        services.AddScoped<IStandingsCalculator, StandingsCalculator>();
        services.AddScoped<ICourtStatusService, CourtStatusService>();

        return services;
    }

    private static bool IsLibSqlConnection(string connection)
    {
        return connection.StartsWith("libsql://", StringComparison.OrdinalIgnoreCase)
            || connection.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || connection.Contains("authToken=", StringComparison.OrdinalIgnoreCase)
            || connection.Contains("jwt=", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveConnectionString(IConfiguration config)
    {
        var connection = config.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(connection))
        {
            return connection;
        }

        var tursoUrl = config["Turso:Url"];
        if (string.IsNullOrWhiteSpace(tursoUrl))
        {
            throw new InvalidOperationException(
                "Either ConnectionStrings:Default or Turso:Url must be configured.");
        }

        var token = config["Turso:AuthToken"];
        if (string.IsNullOrWhiteSpace(token))
        {
            return tursoUrl;
        }

        var separator = tursoUrl.Contains('?') ? "&" : "?";
        return $"{tursoUrl}{separator}authToken={token}";
    }
}
