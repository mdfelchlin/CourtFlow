using Bunit;
using Bunit.TestDoubles;

using CourtFlow.Domain;
using CourtFlow.Web.Components;

using Microsoft.Extensions.DependencyInjection;

namespace CourtFlow.Web.Tests;

public class AccessRedirectTests : AccessTestContext
{
    [Fact]
    public void SignedOutGoesToSignInWithTheCurrentPath()
    {
        SignInAs(TestAccess.SignedOut);
        Navigation.NavigateTo("/tournaments/abc?tab=teams");

        Render<AccessRedirect>();

        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        Assert.Equal(TestAccess.Login("/tournaments/abc?tab=teams"), navigation.Uri);
        Assert.True(navigation.History.First().Options.ForceLoad);
    }

    [Fact]
    public void WithoutAMembershipGoesToNoAccess()
    {
        SignInAs(TestAccess.NoMembership());
        Navigation.NavigateTo("/tournaments");

        Render<AccessRedirect>();

        Assert.EndsWith("/no-access", Navigation.Uri);
    }

    [Fact]
    public void ADeactivatedOrganizationGoesToNoAccess()
    {
        SignInAs(TestAccess.Member(OrganizationRole.Owner, status: OrganizationStatus.Deactivated));
        Navigation.NavigateTo("/teams");

        Render<AccessRedirect>();

        Assert.EndsWith("/no-access", Navigation.Uri);
    }

    [Fact]
    public void AForbiddenRoleSeesNotFound()
    {
        SignInAs(TestAccess.Member(OrganizationRole.Viewer));
        Navigation.NavigateTo("/organization");

        var cut = Render<AccessRedirect>();

        Assert.NotNull(cut.Find("[data-testid=not-found-content]"));
        Assert.EndsWith("/organization", Navigation.Uri);
    }

    [Fact]
    public void AMemberWhoIsNotAnOperatorSeesNotFoundOnOperatorPages()
    {
        SignInAs(TestAccess.NoMembership());
        Navigation.NavigateTo("/operator/requests");

        var cut = Render<AccessRedirect>();

        Assert.NotNull(cut.Find("[data-testid=not-found-content]"));
    }
}