using CourtFlow.Application.Access;
using CourtFlow.Infrastructure.WorkOs;
using Microsoft.Extensions.Logging.Abstractions;

namespace CourtFlow.Api.Tests;

public sealed class WorkOsGatewayTests
{
    [Fact]
    public void Sign_in_url_opens_the_sign_in_screen_rather_than_the_waitlist()
    {
        var options = new WorkOsOptions { ClientId = "client_123", RedirectUri = "https://localhost:7011/auth/callback" };
        using var http = new HttpClient();
        var gateway = new WorkOsGateway(http, options, new WebOptions(), NullLogger<WorkOsGateway>.Instance);

        var url = new Uri(gateway.GetSignInUrl("state-1"));

        Assert.Equal("https://api.workos.com/user_management/authorize", url.GetLeftPart(UriPartial.Path));
        var query = System.Web.HttpUtility.ParseQueryString(url.Query);
        Assert.Equal("sign-in", query["screen_hint"]);
        Assert.Equal("authkit", query["provider"]);
        Assert.Equal("client_123", query["client_id"]);
        Assert.Equal("https://localhost:7011/auth/callback", query["redirect_uri"]);
        Assert.Equal("state-1", query["state"]);
    }
}
