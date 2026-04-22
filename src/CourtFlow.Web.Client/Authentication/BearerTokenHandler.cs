using System.Net.Http.Headers;

namespace CourtFlow.Web.Client.Authentication;

public sealed class BearerTokenHandler(AuthTokenStore tokenStore) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var snapshot = await tokenStore.RefreshFromClerkAsync();
        if (!string.IsNullOrWhiteSpace(snapshot?.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", snapshot.AccessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
