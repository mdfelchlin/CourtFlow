using Bunit;
using CourtFlow.Domain;
using CourtFlow.Web.Pages;

namespace CourtFlow.Web.Tests;

public class NoAccessPageTests : AccessTestContext
{
    [Fact]
    public void Without_a_membership_it_explains_the_invitation_email()
    {
        SignInAs(TestAccess.NoMembership());

        var cut = Render<NoAccess>();

        Assert.Equal("You don't have access yet", cut.Find("h1").TextContent.Trim());
        Assert.Equal(NoAccess.NoMembership, cut.Find("[data-testid=no-access-reason]").TextContent.Trim());
    }

    [Fact]
    public void A_deactivated_organization_is_named()
    {
        SignInAs(TestAccess.Member(OrganizationRole.Editor, status: OrganizationStatus.Deactivated));

        var cut = Render<NoAccess>();

        Assert.Equal(
            "Test Organization has been deactivated. If you think this is a mistake, contact mdfelchlin@gmail.com.",
            cut.Find("[data-testid=no-access-reason]").TextContent.Trim());
    }

    [Fact]
    public void It_offers_request_access_and_sign_out_and_loads_nothing()
    {
        SignInAs(TestAccess.NoMembership());

        var cut = Render<NoAccess>();

        Assert.Equal("/request-access", cut.Find("[data-testid=no-access-request]").GetAttribute("href"));
        Assert.Equal(TestAccess.Logout, cut.Find("[data-testid=no-access-sign-out]").GetAttribute("href"));
        Assert.Empty(Api.AccessCalls);
    }
}
