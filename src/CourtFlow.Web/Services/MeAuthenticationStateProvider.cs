using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace CourtFlow.Web.Services;

/// <summary>
/// Who is signed in, read from <c>GET /api/me</c> once at startup and again after anything that changes it.
/// It has its own client without <see cref="AccessStatusHandler"/>, which refreshes it.
/// </summary>
public sealed class MeAuthenticationStateProvider(HttpClient http) : AuthenticationStateProvider
{
    private Task<AuthenticationState>? _state;

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _state ??= LoadAsync();

    public Task RefreshAsync()
    {
        _state = LoadAsync();
        NotifyAuthenticationStateChanged(_state);
        return _state;
    }

    private async Task<AuthenticationState> LoadAsync()
    {
        try
        {
            var me = await http.GetFromJsonAsync<MeResponse>("api/me") ?? SignedOut;
            return new AuthenticationState(AccessClaims.Principal(me));
        }
        catch (HttpRequestException)
        {
            // With the API unreachable, the app shows its signed-out frame; any director page then asks to sign in.
            return new AuthenticationState(AccessClaims.Principal(SignedOut));
        }
    }

    private static readonly MeResponse SignedOut = new(false, null, null, false, null);
}
