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

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme);

        // MUST register under IConfigureOptions<JwtBearerOptions> (NOT IConfigureNamedOptions).
        // OptionsFactory enumerates IEnumerable<IConfigureOptions<T>> and casts each entry to
        // IConfigureNamedOptions<T> when calling. Registering under the named interface alone
        // makes the singleton unreachable — the factory never sees it, leaving Authority and
        // ValidIssuers unset (IDX10204 in logs). The class still implements
        // IConfigureNamedOptions<JwtBearerOptions> so the cast inside the factory still works.
        // Registered after AddJwtBearer so our Configure runs last (registration-order wins).
        builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>>(sp =>
            new ConfigureCourtFlowJwtBearerOptions(
                sp.GetRequiredService<IOptionsMonitor<ClerkOptions>>(),
                sp.GetRequiredService<IHostEnvironment>(),
                sp.GetRequiredService<ILoggerFactory>()));

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
        if (app.Environment.IsDevelopment())
        {
            app.Use(async (ctx, next) =>
            {
                var path = ctx.Request.Path.Value ?? string.Empty;
                if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                {
                    var log = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("CourtFlow.AuthDebug");
                    var authHeader = ctx.Request.Headers.Authorization.ToString();
                    var hasBearer = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
                    var isAuth = ctx.User?.Identity?.IsAuthenticated == true;
                    var orgId = ctx.User?.FindFirst("org_id")?.Value ?? ctx.User?.FindFirst("orgId")?.Value;
                    log.LogWarning(
                        "Pre-authorize {Method} {Path}: bearer header={HasBearer}, IsAuthenticated={IsAuth}, org_id={OrgId}",
                        ctx.Request.Method,
                        ctx.Request.Path,
                        hasBearer,
                        isAuth,
                        orgId ?? "(none)");
                }

                await next();
            });
        }

        app.UseAuthorization();
        app.MapControllers();

        await app.RunAsync();
    }
}

internal sealed class ConfigureCourtFlowJwtBearerOptions(
    IOptionsMonitor<ClerkOptions> clerkMonitor,
    IHostEnvironment env,
    ILoggerFactory loggerFactory)
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
        // In Development surface the actual validation failure in the WWW-Authenticate
        // header so the browser/Refit client can show the real reason (expired, bad
        // audience, bad issuer, signature failure, etc.) instead of an opaque 401.
        options.IncludeErrorDetails = env.IsDevelopment();

        // Merge into JwtBearer's TokenValidationParameters — do not replace the object
        // JwtBearer wired (ConfigurationManager, signing keys from metadata, etc.).
        options.TokenValidationParameters ??= new TokenValidationParameters();
        var tvp = options.TokenValidationParameters;
        tvp.ValidateIssuer = true;
        tvp.ValidIssuer = null;
        tvp.ValidIssuers = new[] { authority };
        tvp.ValidateAudience = !string.IsNullOrWhiteSpace(clerk.Audience);
        tvp.ValidAudience = clerk.Audience;
        tvp.ValidateLifetime = true;
        tvp.ValidateIssuerSigningKey = true;
        tvp.ClockSkew = TimeSpan.FromMinutes(1);
        tvp.NameClaimType = ClaimTypes.NameIdentifier;
        tvp.RoleClaimType = ClaimTypes.Role;

        var log = loggerFactory.CreateLogger("CourtFlow.Auth.JwtBearer");
        log.LogInformation(
            "JWT bearer configured: Authority={Authority}; ValidIssuers=[{ValidIssuers}]; ValidAudience={Audience}",
            options.Authority,
            string.Join(", ", options.TokenValidationParameters.ValidIssuers ?? []),
            options.TokenValidationParameters.ValidAudience);

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                // Ground-truth diagnostics: decode the raw bearer token's iss claim
                // and dump the currently-effective ValidIssuers so we can see exactly
                // what the validator was comparing against when it rejected us.
                string? rawIss = null;
                int? rawIssLen = null;
                try
                {
                    var auth = ctx.HttpContext.Request.Headers.Authorization.ToString();
                    if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var token = auth["Bearer ".Length..].Trim();
                        var parts = token.Split('.');
                        if (parts.Length >= 2)
                        {
                            var payloadJson = System.Text.Encoding.UTF8.GetString(
                                Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(parts[1]));
                            using var doc = System.Text.Json.JsonDocument.Parse(payloadJson);
                            if (doc.RootElement.TryGetProperty("iss", out var issEl))
                            {
                                rawIss = issEl.GetString();
                                rawIssLen = rawIss?.Length;
                            }
                        }
                    }
                }
                catch { /* best-effort diagnostics */ }

                var effectiveValidIssuers = ctx.Options.TokenValidationParameters.ValidIssuers;
                log.LogWarning(ctx.Exception,
                    "JWT auth failed on {Method} {Path} — {ExceptionType}: {Message} | token.iss=[{TokenIss}] len={TokenIssLen} | ValidIssuers=[{ValidIssuers}] | Authority=[{Authority}]",
                    ctx.HttpContext.Request.Method,
                    ctx.HttpContext.Request.Path,
                    ctx.Exception.GetType().Name,
                    ctx.Exception.Message,
                    rawIss,
                    rawIssLen,
                    effectiveValidIssuers is null ? "(null)" : string.Join(" | ", effectiveValidIssuers),
                    ctx.Options.Authority);
                return Task.CompletedTask;
            },
            OnChallenge = ctx =>
            {
                if (!string.IsNullOrEmpty(ctx.Error) || !string.IsNullOrEmpty(ctx.ErrorDescription))
                {
                    log.LogWarning(
                        "JWT challenge on {Method} {Path}: error={Error}; description={Description}",
                        ctx.HttpContext.Request.Method,
                        ctx.HttpContext.Request.Path,
                        ctx.Error,
                        ctx.ErrorDescription);
                }
                else
                {
                    var hasHeader = ctx.HttpContext.Request.Headers.ContainsKey("Authorization");
                    log.LogWarning(
                        "JWT challenge on {Method} {Path}: no Authorization header present (header sent: {HasHeader})",
                        ctx.HttpContext.Request.Method,
                        ctx.HttpContext.Request.Path,
                        hasHeader);
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                log.LogDebug(
                    "JWT validated on {Method} {Path} — sub={Sub}; org_id={OrgId}; aud={Aud}; iss={Iss}",
                    ctx.HttpContext.Request.Method,
                    ctx.HttpContext.Request.Path,
                    ctx.Principal?.FindFirst("sub")?.Value,
                    ctx.Principal?.FindFirst("org_id")?.Value ?? ctx.Principal?.FindFirst("orgId")?.Value,
                    ctx.SecurityToken is Microsoft.IdentityModel.JsonWebTokens.JsonWebToken jwt
                        ? string.Join(",", jwt.Audiences)
                        : null,
                    (ctx.SecurityToken as Microsoft.IdentityModel.JsonWebTokens.JsonWebToken)?.Issuer);
                return Task.CompletedTask;
            }
        };
    }
}
