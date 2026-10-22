using System.Net;
using System.Web;

using CourtFlow.Api.Access;
using CourtFlow.Application.Access;

using Microsoft.AspNetCore.Mvc.Testing;

namespace CourtFlow.Api.Tests;

[Collection(ApiFixture.Name)]
public sealed class AuthEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private const string WebBase = "https://localhost:7211";

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        // The cookie handler checks expiry against the real clock, so the session must be issued near it.
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        factory.WorkOs.Codes["good"] = new WorkOsSignIn("user_casey", "Casey@Club.test", "Casey", "Jones", "sess_1", "rt_1");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task LoginRedirectsToWorkOSAndSetsTheStateCookie()
    {
        var response = await Browser().GetAsync("/auth/login?returnUrl=%2Fteams", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(FakeWorkOs.AuthorizeUrl, response.Headers.Location!.ToString());
        var cookie = SetCookie(response, AuthEndpoints.StateCookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ACallbackWithABadStateIsRefused()
    {
        var (_, stateCookie) = await StartAsync("/teams");

        var response = await SendWithCookieAsync("/auth/callback?code=good&state=wrong", stateCookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(item => item.StartsWith(SessionCookie.Name + "=", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ACallbackWithoutTheStateCookieIsRefused()
    {
        var (state, _) = await StartAsync("/teams");

        var response = await Browser().GetAsync($"/auth/callback?code=good&state={state}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AGoodCallbackSetsTheSessionCookieAndLands()
    {
        var (state, stateCookie) = await StartAsync("/request-access");

        var response = await SendWithCookieAsync($"/auth/callback?code=good&state={state}", stateCookie);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"{WebBase}/request-access", response.Headers.Location!.ToString());
        var cookie = SetCookie(response, SessionCookie.Name);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        var expires = DateTimeOffset.Parse(
            cookie.Split(';').Select(part => part.Trim())
                .Single(part => part.StartsWith("expires=", StringComparison.OrdinalIgnoreCase))["expires=".Length..],
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(expires, factory.Clock.GetUtcNow().AddDays(30).AddMinutes(-1), factory.Clock.GetUtcNow().AddDays(30).AddMinutes(1));
    }

    [Fact]
    public async Task APersonWithoutAccessLandsOnNoAccess()
    {
        var (state, stateCookie) = await StartAsync("/teams");

        var response = await SendWithCookieAsync($"/auth/callback?code=good&state={state}", stateCookie);

        Assert.Equal($"{WebBase}/no-access", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task AnOutsideReturnUrlFallsBack()
    {
        var (state, stateCookie) = await StartAsync("https://evil.example/");

        var response = await SendWithCookieAsync($"/auth/callback?code=good&state={state}", stateCookie);

        Assert.Equal($"{WebBase}/no-access", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task LogoutClearsTheCookieAndGoesToWorkOS()
    {
        var (state, stateCookie) = await StartAsync("/teams");
        var signedIn = await SendWithCookieAsync($"/auth/callback?code=good&state={state}", stateCookie);
        var session = CookiePair(SetCookie(signedIn, SessionCookie.Name));

        var me = await SendWithCookieAsync("/api/me", session);
        Assert.Contains("\"signedIn\":true", await me.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var response = await SendWithCookieAsync("/auth/logout", session);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"{FakeWorkOs.LogoutUrl}?session_id=sess_1", response.Headers.Location!.ToString());
        Assert.Contains("expires=Thu, 01 Jan 1970", SetCookie(response, SessionCookie.Name), StringComparison.OrdinalIgnoreCase);
    }

    private HttpClient Browser() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = false,
    });

    private async Task<(string State, string Cookie)> StartAsync(string returnUrl)
    {
        var response = await Browser().GetAsync($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}", TestContext.Current.CancellationToken);
        var state = HttpUtility.ParseQueryString(response.Headers.Location!.Query)["state"]!;
        return (state, CookiePair(SetCookie(response, AuthEndpoints.StateCookie)));
    }

    private async Task<HttpResponseMessage> SendWithCookieAsync(string path, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", cookie);
        return await Browser().SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string SetCookie(HttpResponseMessage response, string name) =>
        response.Headers.GetValues("Set-Cookie").Single(cookie => cookie.StartsWith(name + "=", StringComparison.Ordinal));

    private static string CookiePair(string setCookie) => setCookie.Split(';')[0];
}