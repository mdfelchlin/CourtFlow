using System.Security.Claims;
using System.Text.Encodings.Web;
using CourtFlow.Api.Access;
using CourtFlow.Infrastructure.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CourtFlow.Api.Tests;

public sealed record TestUser(string UserId, string Email, string Name)
{
    public static TestUser Named(string key) => new($"user_{key}", $"{key}@courtflow.test", $"Test {key}");
}

/// <summary>
/// Signs a request in from <c>X-Test-*</c> headers so tests need no WorkOS round trip. Without them it reads the
/// session cookie, so the sign-in endpoints are tested as they run.
/// </summary>
public sealed class TestSignIn(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";
    public const string EmailHeader = "X-Test-Email";
    public const string NameHeader = "X-Test-Name";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers[UserHeader].ToString();
        if (string.IsNullOrEmpty(userId))
            return Context.AuthenticateAsync(SessionCookie.Scheme);

        List<Claim> claims =
        [
            new(AccessClaims.UserId, userId),
            new(AccessClaims.Email, Request.Headers[EmailHeader].ToString()),
            new(AccessClaims.Name, Request.Headers[NameHeader].ToString()),
        ];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }

    public static void Apply(HttpClient client, TestUser user)
    {
        client.DefaultRequestHeaders.Add(UserHeader, user.UserId);
        client.DefaultRequestHeaders.Add(EmailHeader, user.Email);
        client.DefaultRequestHeaders.Add(NameHeader, user.Name);
    }
}
