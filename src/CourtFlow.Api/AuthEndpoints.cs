using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using CourtFlow.Api.Access;
using CourtFlow.Application.Access;
using CourtFlow.Infrastructure.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;

namespace CourtFlow.Api;

/// <summary>Browser navigations, not API calls: each one ends in a redirect.</summary>
public static class AuthEndpoints
{
    public const string StateCookie = "courtflow-signin";
    public const string SignInFailed = "Sign-in could not be completed. Please try again.";

    private const string ProtectorPurpose = "CourtFlow.SignIn";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    private sealed record SignInState(string State, string? ReturnUrl);

    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth").AllowAnonymous();

        group.MapGet("/login", (string? returnUrl, HttpContext context, IWorkOs workOs, IDataProtectionProvider protection) =>
        {
            var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            var payload = JsonSerializer.Serialize(new SignInState(state, ReturnUrl.IsLocal(returnUrl) ? returnUrl : null));
            context.Response.Cookies.Append(StateCookie, protection.CreateProtector(ProtectorPurpose).Protect(payload), new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/auth",
                MaxAge = StateLifetime,
                IsEssential = true,
            });
            return Results.Redirect(workOs.GetSignInUrl(state));
        });

        group.MapGet("/callback", async (
            string? code,
            string? state,
            string? error,
            HttpContext context,
            IWorkOs workOs,
            IDataProtectionProvider protection,
            SignInUseCases signIn,
            CurrentAccess access,
            WebOptions web,
            TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var saved = ReadState(context, protection);
            context.Response.Cookies.Delete(StateCookie, new CookieOptions { Path = "/auth", Secure = true });

            if (!string.IsNullOrEmpty(error))
                return Results.Redirect(web.Link("/"));
            if (saved is null || string.IsNullOrEmpty(code) || !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(saved.State), System.Text.Encoding.UTF8.GetBytes(state ?? "")))
                return Results.BadRequest(SignInFailed);

            WorkOsSignIn person;
            try
            {
                person = await workOs.AuthenticateWithCodeAsync(code, cancellationToken);
            }
            catch (WorkOsUnavailableException)
            {
                return Results.Text(SignInFailed, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var name = person.Name.Length > 0 ? person.Name : person.Email;
            await signIn.OnSignedInAsync(person.UserId, person.Email, name, cancellationToken);

            List<Claim> claims =
            [
                new(AccessClaims.UserId, person.UserId),
                new(AccessClaims.Email, person.Email),
                new(AccessClaims.Name, name),
            ];
            if (!string.IsNullOrEmpty(person.SessionId))
                claims.Add(new Claim(AccessClaims.SessionId, person.SessionId));

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SessionCookie.Scheme));
            await context.SignInAsync(SessionCookie.Scheme, principal, SessionCookie.NewProperties(person.RefreshToken, clock.GetUtcNow()));

            await access.LoadAsync(person.UserId, person.Email, name, cancellationToken);
            return Results.Redirect(web.Link(SignInUseCases.LandingFor(access, saved.ReturnUrl)));
        });

        group.MapGet("/logout", async (HttpContext context, IWorkOs workOs, WebOptions web) =>
        {
            var sessionId = context.User.FindFirst(AccessClaims.SessionId)?.Value;
            await context.SignOutAsync(SessionCookie.Scheme);
            return Results.Redirect(string.IsNullOrEmpty(sessionId) ? web.Link("/") : workOs.GetLogoutUrl(sessionId));
        });
    }

    private static SignInState? ReadState(HttpContext context, IDataProtectionProvider protection)
    {
        if (!context.Request.Cookies.TryGetValue(StateCookie, out var cookie) || string.IsNullOrEmpty(cookie))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SignInState>(protection.CreateProtector(ProtectorPurpose).Unprotect(cookie));
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            return null;
        }
    }
}
