using System.Net;
using System.Net.Http.Headers;

namespace CourtFlow.Web.Client.Authentication;

/// <summary>
/// Attaches the current Clerk-issued access token to outbound API requests and
/// performs a single refresh-and-retry on a <c>401 Unauthorized</c> response.
/// The retry exists to defeat the WASM-hydration race: pages can fire API calls
/// before the Clerk JS bundle has finished initializing, which yields a no-token
/// first request. When the server says 401 we ask <see cref="AuthTokenStore"/>
/// to round-trip Clerk again — by then it's usually ready — and replay the
/// request with the fresh token.
/// </summary>
public sealed class BearerTokenHandler(AuthTokenStore tokenStore) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await AttachTokenAsync(request);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // Single retry: dispose the failed response, give Clerk a beat to publish
        // the session it didn't have yet, then replay.
        response.Dispose();
        await Task.Delay(150, cancellationToken);

        var retry = await CloneAsync(request, cancellationToken);
        await AttachTokenAsync(retry, forceRefresh: true);
        return await base.SendAsync(retry, cancellationToken);
    }

    private async Task AttachTokenAsync(HttpRequestMessage request, bool forceRefresh = false)
    {
        var snapshot = forceRefresh
            ? await tokenStore.RefreshFromClerkAsync()
            : (await tokenStore.RefreshFromClerkAsync() ?? await tokenStore.GetAsync());
        if (!string.IsNullOrWhiteSpace(snapshot?.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", snapshot.AccessToken);
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
