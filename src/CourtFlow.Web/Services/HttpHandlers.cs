using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace CourtFlow.Web.Services;

/// <summary>Sends the API's session cookie with every call, though the API is on another origin.</summary>
public sealed class IncludeCredentialsHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// Turns the API's access answers into navigation: a <c>401</c> goes to sign-in, and losing the organization goes to
/// the no-access page. The call is then cancelled, which Blazor ignores, so the page that made it shows no error.
/// </summary>
public sealed class AccessStatusHandler(NavigationManager navigation, SignInLinks links, MeAuthenticationStateProvider me) : DelegatingHandler
{
    public const string NoAccessPath = "/no-access";

    private static readonly string[] LostAccess = ["no_access", "organization_deactivated"];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            navigation.NavigateTo(links.Login(CurrentPath(navigation)), forceLoad: true);
            throw new OperationCanceledException("Signed out; going to sign-in.");
        }

        if (response.StatusCode == HttpStatusCode.Forbidden
            && await ReasonAsync(response, cancellationToken) is { } reason
            && LostAccess.Contains(reason))
        {
            await me.RefreshAsync();
            navigation.NavigateTo(NoAccessPath);
            throw new OperationCanceledException("Access ended; going to the no-access page.");
        }

        return response;
    }

    public static string CurrentPath(NavigationManager navigation) => "/" + navigation.ToBaseRelativePath(navigation.Uri);

    private static async Task<string?> ReasonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            // Buffered so the caller can still read the body for a plain "forbidden".
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            return (await response.Content.ReadFromJsonAsync<Refusal>(cancellationToken))?.Reason;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private sealed record Refusal(string? Reason);
}