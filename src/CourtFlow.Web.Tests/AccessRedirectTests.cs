using Bunit;
using Bunit.TestDoubles;
using CourtFlow.Domain;
using CourtFlow.Web.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CourtFlow.Web.Tests;

public class AccessRedirectTests : AccessTestContext
{
    [Fact]
    public void Signed_out_goes_to_sign_in_with_the_current_path()
    {
        SignInAs(TestAccess.SignedOut);
        Navigation.NavigateTo("/tournaments/abc?tab=teams");

        Render<AccessRedirect>();

        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        Assert.Equal(TestAccess.Login("/tournaments/abc?tab=teams"), navigation.Uri);
        Assert.True(navigation.History.First().Options.ForceLoad);
    }

    [Fact]
    public void Without_a_membership_goes_to_no_access()
    {
        SignInAs(TestAccess.NoMembership());
        Navigation.NavigateTo("/tournaments");

        Render<AccessRedirect>();

        Assert.EndsWith("/no-access", Navigation.Uri);
    }

    [Fact]
    public void A_deactivated_organization_goes_to_no_access()
    {
        SignInAs(TestAccess.Member(OrganizationRole.Owner, status: OrganizationStatus.Deactivated));
        Navigation.NavigateTo("/teams");

        Render<AccessRedirect>();

        Assert.EndsWith("/no-access", Navigation.Uri);
    }

    [Fact]
    public void A_forbidden_role_sees_not_found()
    {
        SignInAs(TestAccess.Member(OrganizationRole.Viewer));
        Navigation.NavigateTo("/organization");

        var cut = Render<AccessRedirect>();

        Assert.NotNull(cut.Find("[data-testid=not-found-content]"));
        Assert.EndsWith("/organization", Navigation.Uri);
    }

    [Fact]
    public void A_member_who_is_not_an_operator_sees_not_found_on_operator_pages()
    {
        SignInAs(TestAccess.NoMembership());
        Navigation.NavigateTo("/operator/requests");

        var cut = Render<AccessRedirect>();

        Assert.NotNull(cut.Find("[data-testid=not-found-content]"));
    }
}
