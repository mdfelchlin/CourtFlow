using Microsoft.JSInterop;

namespace CourtFlow.Web.Client.Authentication;

public sealed class AuthTokenStore(IJSRuntime jsRuntime)
{
    private const string AccessTokenKey = "courtflow.accessToken";
    private const string AccessTokenExpiresKey = "courtflow.accessTokenExpiresUtc";
    private const string UserEmailKey = "courtflow.userEmail";
    private const string UserIdKey = "courtflow.userId";
    private const string OrgIdKey = "courtflow.orgId";
    private const string RoleKey = "courtflow.role";
    public async Task SaveAsync(AuthTokenSnapshot snapshot)
    {
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", AccessTokenKey, snapshot.AccessToken);
        if (!string.IsNullOrWhiteSpace(snapshot.Email))
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", UserEmailKey, snapshot.Email);
        if (snapshot.ExpiresUtc is not null)
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", AccessTokenExpiresKey, snapshot.ExpiresUtc.Value.ToString("O"));
        if (!string.IsNullOrWhiteSpace(snapshot.UserId))
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", UserIdKey, snapshot.UserId);
        if (snapshot.OrganizationId is not null)
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", OrgIdKey, snapshot.OrganizationId.Value.ToString());
        if (!string.IsNullOrWhiteSpace(snapshot.Role))
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", RoleKey, snapshot.Role);
    }

    public async Task ClearAsync()
    {
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", AccessTokenKey);
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", AccessTokenExpiresKey);
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", UserEmailKey);
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", UserIdKey);
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", OrgIdKey);
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", RoleKey);
    }

    public async Task<AuthTokenSnapshot?> RefreshFromClerkAsync(string? publishableKey = null, string? frontendApi = null)
    {
        ClerkSessionInfo? sessionInfo;
        try
        {
            sessionInfo = await jsRuntime.InvokeAsync<ClerkSessionInfo?>(
                "courtflowClerk.getSessionInfo",
                publishableKey,
                frontendApi);
        }
        catch
        {
            return await GetAsync();
        }

        if (sessionInfo is null || string.IsNullOrWhiteSpace(sessionInfo.Token))
        {
            await ClearAsync();
            return null;
        }

        DateTimeOffset? expiresUtc = DateTimeOffset.TryParse(sessionInfo.ExpiresUtc, out var parsedExpires)
            ? parsedExpires
            : null;
        Guid? organizationId = Guid.TryParse(sessionInfo.OrganizationId, out var parsedOrgId)
            ? parsedOrgId
            : null;

        var snapshot = new AuthTokenSnapshot(
            sessionInfo.Token,
            expiresUtc,
            sessionInfo.Email,
            UserId: string.IsNullOrWhiteSpace(sessionInfo.UserId) ? null : sessionInfo.UserId,
            organizationId,
            sessionInfo.Role);

        await SaveAsync(snapshot);
        return snapshot;
    }

    public async Task<AuthTokenSnapshot?> GetAsync()
    {
        var token = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", AccessTokenKey);
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var expiresRaw = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", AccessTokenExpiresKey);
        var email = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", UserEmailKey);
        var userIdRaw = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", UserIdKey);
        var orgIdRaw = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", OrgIdKey);
        var role = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", RoleKey);

        DateTimeOffset? expiresUtc = DateTimeOffset.TryParse(expiresRaw, out var parsedExpires) ? parsedExpires : null;
        Guid? orgId = Guid.TryParse(orgIdRaw, out var parsedOrgId) ? parsedOrgId : null;

        return new AuthTokenSnapshot(token, expiresUtc, email,
            UserId: string.IsNullOrWhiteSpace(userIdRaw) ? null : userIdRaw, orgId, role);
    }
}

public sealed record AuthTokenSnapshot(
    string AccessToken,
    DateTimeOffset? ExpiresUtc,
    string? Email,
    string? UserId,
    Guid? OrganizationId,
    string? Role);

public sealed record ClerkSessionInfo(
    string Token,
    string? Email,
    string? UserId,
    string? OrganizationId,
    string? Role,
    string? ExpiresUtc);
