using System.Net;
using System.Net.Http.Headers;
using Microsoft.JSInterop;

namespace CourtFlow.Web.Client.Authentication;

/// <summary>
/// Attaches the current Clerk-issued access token to outbound API requests.
///
/// Two safety nets defeat the WASM-hydration race where a page can fire its
/// first API call before <c>clerk-js</c> has finished initializing:
///
/// 1. If the first refresh comes back without a token, wait briefly and try
///    again (up to <see cref="PreFlightAttempts"/> attempts) <i>before</i>
///    sending the request. This keeps the no-token-yet condition from ever
///    becoming a 401 round-trip.
/// 2. If the server still says 401 despite our pre-flight, refresh the token
///    once more and replay the request. This catches the case where Clerk
///    had a token in localStorage but it had expired.
///
/// Every step logs a one-liner to the JS console (prefixed
/// <c>[CourtFlow.Auth]</c>) so the failure mode is visible in browser
/// DevTools without needing server logs.
/// </summary>
public sealed class BearerTokenHandler : DelegatingHandler
{
    private const int PreFlightAttempts = 4;
    private static readonly TimeSpan PreFlightDelay = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);

    private readonly AuthTokenStore _tokenStore;
    private readonly IJSRuntime _js;

    public BearerTokenHandler(AuthTokenStore tokenStore, IJSRuntime js)
    {
        _tokenStore = tokenStore;
        _js = js;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var path = request.RequestUri?.AbsolutePath ?? "(unknown)";

        var token = await AcquireTokenWithRetryAsync(requestId, path, cancellationToken);
        AttachToken(request, token);
        await ConsoleLogAsync($"[{requestId}] -> {request.Method} {path} (auth={(token is null ? "anonymous" : "bearer")})");

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            await ConsoleLogAsync($"[{requestId}] <- {(int)response.StatusCode} {request.Method} {path}");
            return response;
        }

        await ConsoleLogAsync($"[{requestId}] <- 401 {request.Method} {path} — refreshing token and retrying once");
        response.Dispose();

        await Task.Delay(RetryDelay, cancellationToken);
        var freshSnapshot = await _tokenStore.RefreshFromClerkAsync();
        var retry = await CloneAsync(request, cancellationToken);
        AttachToken(retry, freshSnapshot?.AccessToken);
        await ConsoleLogAsync($"[{requestId}] -> retry {request.Method} {path} (auth={(freshSnapshot?.AccessToken is null ? "anonymous" : "bearer")})");

        var retryResponse = await base.SendAsync(retry, cancellationToken);
        await ConsoleLogAsync($"[{requestId}] <- {(int)retryResponse.StatusCode} retry {request.Method} {path}");
        return retryResponse;
    }

    private async Task<string?> AcquireTokenWithRetryAsync(string requestId, string path, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= PreFlightAttempts; attempt++)
        {
            AuthTokenSnapshot? snapshot;
            try
            {
                snapshot = await _tokenStore.RefreshFromClerkAsync();
            }
            catch (Exception ex)
            {
                await ConsoleLogAsync($"[{requestId}] token refresh threw ({ex.GetType().Name}: {ex.Message}) on attempt {attempt}");
                snapshot = null;
            }
            if (!string.IsNullOrWhiteSpace(snapshot?.AccessToken))
                return snapshot.AccessToken;

            // Fallback: maybe Clerk briefly failed but localStorage still has a usable token.
            try
            {
                var cached = await _tokenStore.GetAsync();
                if (!string.IsNullOrWhiteSpace(cached?.AccessToken))
                {
                    await ConsoleLogAsync($"[{requestId}] using cached token on attempt {attempt}");
                    return cached.AccessToken;
                }
            }
            catch
            {
                // Ignore — fall through to the wait.
            }

            if (attempt < PreFlightAttempts)
            {
                await ConsoleLogAsync($"[{requestId}] no token yet on attempt {attempt}/{PreFlightAttempts} for {path} — waiting {PreFlightDelay.TotalMilliseconds:N0}ms");
                await Task.Delay(PreFlightDelay, cancellationToken);
            }
        }

        await ConsoleLogAsync($"[{requestId}] giving up after {PreFlightAttempts} attempts — sending {path} anonymous");
        return null;
    }

    private static void AttachToken(HttpRequestMessage request, string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task ConsoleLogAsync(string message)
    {
        try
        {
            await _js.InvokeVoidAsync("console.log", $"[CourtFlow.Auth] {message}");
        }
        catch
        {
            // Logging is best-effort; never let it bubble up.
        }
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage source, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri)
        {
            Version = source.Version
        };
        foreach (var header in source.Headers)
        {
            if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                continue;
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        if (source.Content is not null)
        {
            var ms = new MemoryStream();
            await source.Content.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
            var content = new StreamContent(ms);
            foreach (var header in source.Content.Headers)
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            clone.Content = content;
        }
        return clone;
    }
}
