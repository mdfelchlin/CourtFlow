using System.Security.Claims;
using CourtFlow.Web.Shared.Services;
using Microsoft.JSInterop;

namespace CourtFlow.Web.Client.Authentication;

public sealed class AuthSessionService(
    IJSRuntime jsRuntime,
    AuthTokenStore tokenStore,
    JwtAuthenticationStateProvider authStateProvider) : IAuthSessionService
{
    public bool IsAuthenticated => _currentUser.Identity?.IsAuthenticated == true;

    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    public async Task SignOutAsync()
    {
        await jsRuntime.InvokeVoidAsync("courtflowClerk.signOut", null, null);

        await tokenStore.ClearAsync();
        await authStateProvider.MarkLoggedOutAsync();
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
    }

    public async Task<ClaimsPrincipal> GetCurrentUserAsync()
    {
        await tokenStore.RefreshFromClerkAsync();
        _currentUser = (await authStateProvider.GetAuthenticationStateAsync()).User;
        return _currentUser;
    }
}
