using CourtFlow.Application.Access;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CourtFlow.Api.Access;

/// <summary>The API's session: a 30-day cookie from sign-in, renewed against WorkOS every few minutes so a session ended there ends here.</summary>
public static partial class SessionCookie
{
    public const string Name = "courtflow-session";
    public const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;
    public const string RefreshToken = "refresh_token";

    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);
    public static readonly TimeSpan RefreshEvery = TimeSpan.FromMinutes(5);

    private const string SignedInAt = "signed_in_at";
    private const string RefreshedAt = "refreshed_at";

    public static void Configure(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = Name;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = Lifetime;
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = ValidateAsync;
    }

    public static AuthenticationProperties NewProperties(string refreshToken, DateTimeOffset now)
    {
        var properties = new AuthenticationProperties
        {
            IsPersistent = true,
            IssuedUtc = now,
            ExpiresUtc = now + Lifetime,
            AllowRefresh = true,
        };
        properties.StoreTokens([new AuthenticationToken { Name = RefreshToken, Value = refreshToken }]);
        properties.Items[SignedInAt] = now.ToString("O");
        properties.Items[RefreshedAt] = now.ToString("O");
        return properties;
    }

    private static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var now = context.Options.TimeProvider?.GetUtcNow() ?? DateTimeOffset.UtcNow;
        var properties = context.Properties;
        if (Read(properties, SignedInAt) is not { } signedInAt || now >= signedInAt + Lifetime)
        {
            await RejectAsync(context);
            return;
        }

        if (Read(properties, RefreshedAt) is { } refreshedAt && now - refreshedAt < RefreshEvery)
            return;

        var token = properties.GetTokenValue(RefreshToken);
        if (string.IsNullOrEmpty(token))
        {
            await RejectAsync(context);
            return;
        }

        var workOs = context.HttpContext.RequestServices.GetRequiredService<IWorkOs>();
        try
        {
            properties.UpdateTokenValue(RefreshToken, await workOs.RefreshAsync(token, context.HttpContext.RequestAborted));
            properties.Items[RefreshedAt] = now.ToString("O");
            // The handler renews from IssuedUtc to ExpiresUtc, so moving IssuedUtc to now keeps the original expiry.
            properties.IssuedUtc = now;
            context.ShouldRenew = true;
        }
        catch (WorkOsSessionEndedException)
        {
            await RejectAsync(context);
        }
        catch (WorkOsUnavailableException exception)
        {
            LogRefreshKept(
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SessionCookie)),
                exception);
        }
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(Scheme);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "WorkOS could not refresh a session. The session is kept until the next check.")]
    private static partial void LogRefreshKept(ILogger logger, Exception exception);

    private static DateTimeOffset? Read(AuthenticationProperties properties, string key) =>
        properties.Items.TryGetValue(key, out var value) && DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
}