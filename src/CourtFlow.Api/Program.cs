using System.Text.Json.Serialization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using CourtFlow.Api.Infrastructure;
using CourtFlow.Application;
using CourtFlow.Infrastructure;
using CourtFlow.Infrastructure.Persistence;
using CourtFlow.Infrastructure.Seed;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
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

        builder.Services
            .AddCourtFlowApplication()
            .AddCourtFlowInfrastructure(builder.Configuration);

        var clerk = builder.Configuration.GetSection(ClerkOptions.SectionName).Get<ClerkOptions>()
            ?? throw new InvalidOperationException("Clerk configuration is missing.");
        if (!clerk.Enabled && !builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("Clerk must be enabled outside development.");
        }

        if (string.IsNullOrWhiteSpace(clerk.Authority) && !builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("Clerk:Authority must be configured outside development.");
        }

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                if (!clerk.Enabled || string.IsNullOrWhiteSpace(clerk.Authority))
                {
                    // Keep startup unblocked in development even when Clerk config is missing,
                    // but all authenticated requests will still fail until real Clerk values are configured.
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = "https://invalid.local",
                        ValidateAudience = true,
                        ValidAudience = "invalid",
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = false,
                        ClockSkew = TimeSpan.Zero,
                        NameClaimType = ClaimTypes.Name,
                        RoleClaimType = ClaimTypes.Role
                    };
                    return;
                }

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
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };
            });
        builder.Services.AddScoped<IClaimsTransformation, ClerkClaimsTransformation>();

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.RequireAdmin, policy =>
                policy.RequireAuthenticatedUser().RequireRole("Admin"));
            options.AddPolicy(AuthorizationPolicies.RequireOrg, policy =>
                policy.RequireAuthenticatedUser().RequireClaim("org_id"));
        });

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        builder.Services.AddFluentValidationAutoValidation();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo { Title = "CourtFlow API", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
            {
                In = Microsoft.OpenApi.ParameterLocation.Header,
                Type = Microsoft.OpenApi.SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT Authorization header using the Bearer scheme."
            });
            options.AddSecurityRequirement(_ => new Microsoft.OpenApi.OpenApiSecurityRequirement
            {
                [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", null)] = new List<string>()
            });
        });

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(p => p
                .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "https://localhost:7178" })
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
