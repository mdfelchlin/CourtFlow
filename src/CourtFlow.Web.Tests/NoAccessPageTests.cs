using Bunit;

using CourtFlow.Domain;
using CourtFlow.Web.Pages;

namespace CourtFlow.Web.Tests;

public class NoAccessPageTests : AccessTestContext
{
    [Fact]
    public void WithoutAMembershipItExplainsTheInvitationEmail()
    {
        SignInAs(TestAccess.NoMembership());

        var cut = Render<NoAccess>();

        Assert.Equal("You don't have access yet", cut.Find("h1").TextContent.Trim());
        Assert.Equal(NoAccess.NoMembership, cut.Find("[data-testid=no-access-reason]").TextContent.Trim());
    }

    [Fact]
    public void ADeactivatedOrganizationIsNamed()
    {
        SignInAs(TestAccess.Member(OrganizationRole.Editor, status: OrganizationStatus.Deactivated));

        var cut = Render<NoAccess>();

        Assert.Equal(
            "Test Organization has been deactivated. If you think this is a mistake, contact mdfelchlin@gmail.com.",
            cut.Find("[data-testid=no-access-reason]").TextContent.Trim());
    }

    [Fact]
    public void ItOffersRequestAccessAndSignOutAndLoadsNothing()
    {
        SignInAs(TestAccess.NoMembership());

        var cut = Render<NoAccess>();

        Assert.Equal("/request-access", cut.Find("[data-testid=no-access-request]").GetAttribute("href"));
        Assert.Equal(TestAccess.Logout, cut.Find("[data-testid=no-access-sign-out]").GetAttribute("href"));
        Assert.Empty(Api.AccessCalls);
    }
}