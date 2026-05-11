using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CourtFlow.Api.Infrastructure;
using CourtFlow.Application;
using CourtFlow.Infrastructure;
using CourtFlow.Infrastructure.Options;
using CourtFlow.Infrastructure.Persistence;
using CourtFlow.Infrastructure.Seed;
using CourtFlow.ServiceDefaults;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Serilog;

namespace CourtFlow.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog((ctx, cfg) => cfg
            .ReadFrom.Configuration(ctx.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/courtflow-api-.log", rollingInterval: RollingInterval.Day));

        builder.Services.AddCourtFlowApplication();
        builder.AddCourtFlowServiceDefaults();

        builder.Services.AddOptions<ClerkOptions>()
            .Bind(builder.Configuration.GetSection(ClerkOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<ClerkOptions>, ClerkOptionsValidator>();
        builder.Services.AddSingleton<IConfigureNamedOptions<JwtBearerOptions>>(sp =>
            new ConfigureCourtFlowJwtBearerOptions(sp.GetRequiredService<IOptionsMonitor<ClerkOptions>>()));

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme);

        builder.Services.AddScoped<IClaimsTransformation, ClerkClaimsTransformation>();

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.RequireAdmin, policy =>
                policy.RequireAuthenticatedUser().RequireRole("Admin"));
            options.AddPolicy(AuthorizationPolicies.RequireOrg, policy =>
                policy.RequireAuthenticatedUser().RequireClaim("org_id"));
        });

        builder.Services.AddCourtFlowInfrastructure(builder.Configuration);
        builder.Services.AddCourtFlowOutboxDispatcher();

        builder.Services.AddExceptionHandler<CourtFlowProblemExceptionHandler>();

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        builder.Services.AddFluentValidationAutoValidation();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var corsBindings = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>();
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(p => p
                .WithOrigins(corsBindings?.Origins ?? new[] { "https://localhost:7247", "http://localhost:5111" })
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
        });

        builder.Services.AddProblemDetails();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth-signin", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));
            options.AddPolicy("public-read", context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 4,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));
        });

        var app = builder.Build();

        app.UseSerilogRequestLogging();
        app.UseExceptionHandler();
        app.MapCourtFlowHealthChecks();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();

            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            await DemoSeeder.SeedAsync(scope.ServiceProvider);
        }

        app.UseHttpsRedirection();
        app.UseRateLimiter();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await app.RunAsync();
    }
}

internal sealed class ConfigureCourtFlowJwtBearerOptions(IOptionsMonitor<ClerkOptions> clerkMonitor)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(JwtBearerOptions options) =>
        Configure(JwtBearerDefaults.AuthenticationScheme, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (!string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
            return;

        var clerk = clerkMonitor.CurrentValue;
        var authority = clerk.Authority.TrimEnd('/');
        options.Authority = authority;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authority,
            ValidateAudience = !string.IsNullOrWhiteSpace(clerk.Audience),
            ValidAudience = clerk.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = false,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
    }
}
