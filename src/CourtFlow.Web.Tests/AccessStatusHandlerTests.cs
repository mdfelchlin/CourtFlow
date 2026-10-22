using System.Net;
using System.Net.Http.Json;

using Bunit;
using Bunit.TestDoubles;

using CourtFlow.Domain;
using CourtFlow.Web.Services;

using Microsoft.Extensions.DependencyInjection;

namespace CourtFlow.Web.Tests;

public class AccessStatusHandlerTests : BunitContext
{
    private readonly StubHandler _api = new();
    private readonly StubHandler _me = new();
    private readonly MeAuthenticationStateProvider _provider;
    private readonly HttpClient _client;
    private BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

    public AccessStatusHandlerTests()
    {
        _provider = new MeAuthenticationStateProvider(new HttpClient(_me) { BaseAddress = new Uri(TestAccess.ApiBase) });
        var handler = new AccessStatusHandler(Services.GetRequiredService<BunitNavigationManager>(), new SignInLinks(TestAccess.ApiBase), _provider)
        {
            InnerHandler = _api,
        };
        _client = new HttpClient(handler) { BaseAddress = new Uri(TestAccess.ApiBase) };
        _me.Respond = _ => JsonContent.Create(TestAccess.Member(OrganizationRole.Editor));
    }

    [Fact]
    public async Task A401GoesToSignInWithTheCurrentPath()
    {
        Navigation.NavigateTo("/teams");
        _api.Status = HttpStatusCode.Unauthorized;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _client.GetAsync("api/tournaments", Xunit.TestContext.Current.CancellationToken));

        Assert.Equal(TestAccess.Login("/teams"), Navigation.Uri);
        Assert.True(Navigation.History.First().Options.ForceLoad);
    }

    [Theory]
    [InlineData("organization_deactivated")]
    [InlineData("no_access")]
    public async Task LosingTheOrganizationRefreshesMeAndGoesToNoAccess(string reason)
    {
        Navigation.NavigateTo("/tournaments");
        await _provider.GetAuthenticationStateAsync();
        var before = _me.Calls;
        _me.Respond = _ => JsonContent.Create(TestAccess.Member(OrganizationRole.Editor, status: OrganizationStatus.Deactivated));
        _api.Status = HttpStatusCode.Forbidden;
        _api.Respond = _ => JsonContent.Create(new { reason });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _client.GetAsync("api/tournaments", Xunit.TestContext.Current.CancellationToken));

        Assert.EndsWith(AccessStatusHandler.NoAccessPath, Navigation.Uri);
        Assert.Equal(before + 1, _me.Calls);
        var state = await _provider.GetAuthenticationStateAsync();
        Assert.True(state.User.IsDeactivated());
    }

    [Fact]
    public async Task APlainForbiddenReachesTheCallerWithItsBody()
    {
        Navigation.NavigateTo("/tournaments");
        _api.Status = HttpStatusCode.Forbidden;
        _api.Respond = _ => JsonContent.Create(new { reason = "forbidden" });

        var response = await _client.GetAsync("api/tournaments", Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("forbidden", await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken));
        Assert.EndsWith("/tournaments", Navigation.Uri);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public Func<HttpRequestMessage, HttpContent?> Respond { get; set; } = _ => null;
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = Respond(request) ?? new StringContent("") });
        }
    }
}