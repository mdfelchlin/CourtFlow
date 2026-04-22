using System.Security.Claims;
using CourtFlow.Web.Shared.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace CourtFlow.Web.Client.Authentication;

public sealed class JwtAuthenticationStateProvider(AuthTokenStore tokenStore) : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());
    private ClaimsPrincipal? _cachedPrincipal;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var snapshot = await tokenStore.RefreshFromClerkAsync();
            _cachedPrincipal = BuildPrincipal(snapshot);
        }
        catch
        {
            // Never block routing/layout on Clerk or JS interop failures; user can retry sign-in.
            _cachedPrincipal = Anonymous;
        }

        return new AuthenticationState(_cachedPrincipal);
    }

    public async Task MarkAuthenticatedAsync()
    {
        var snapshot = await tokenStore.RefreshFromClerkAsync();
        _cachedPrincipal = BuildPrincipal(snapshot);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_cachedPrincipal)));
    }

    public async Task MarkLoggedOutAsync()
    {
        await tokenStore.ClearAsync();
        _cachedPrincipal = Anonymous;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_cachedPrincipal)));
    }

    private static ClaimsPrincipal BuildPrincipal(AuthTokenSnapshot? snapshot)
    {
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.AccessToken))
        {
            return Anonymous;
        }

        if (snapshot.ExpiresUtc is not null && snapshot.ExpiresUtc <= DateTimeOffset.UtcNow)
        {
            return Anonymous;
        }

        var claims = new List<Claim>();
        if (!string.IsNullOrWhiteSpace(snapshot.Email))
        {
            claims.Add(new Claim(ClaimTypes.Name, snapshot.Email));
            claims.Add(new Claim(ClaimTypes.Email, snapshot.Email));
        }

        if (snapshot.UserId is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, snapshot.UserId.Value.ToString()));
        }

        if (snapshot.OrganizationId is not null)
        {
            claims.Add(new Claim("org_id", snapshot.OrganizationId.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Role))
        {
            claims.Add(new Claim(ClaimTypes.Role, snapshot.Role));
        }

        claims.Add(new Claim("access_token", snapshot.AccessToken));
        if (snapshot.ExpiresUtc is not null)
        {
            claims.Add(new Claim("access_token_expires", snapshot.ExpiresUtc.Value.ToString("O")));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }
}
