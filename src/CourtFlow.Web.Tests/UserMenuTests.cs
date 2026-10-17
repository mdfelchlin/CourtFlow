using Bunit;
using CourtFlow.Domain;
using CourtFlow.Web.Components;
using CourtFlow.Web.Services;

namespace CourtFlow.Web.Tests;

public class UserMenuTests : AccessTestContext
{
    public static TheoryData<string, bool, bool> People => new()
    {
        { "owner", true, false },
        { "editor", false, false },
        { "viewer", false, false },
        { "operator-member", false, true },
        { "operator-only", false, true },
    };

    [Theory]
    [MemberData(nameof(People))]
    public void The_items_follow_the_person(string who, bool showsOrganization, bool showsOperator)
    {
        var menu = RenderMenu(Person(who));

        var items = Popovers.FindAll(".mud-menu-item").Select(item => item.TextContent.Trim()).ToList();
        Assert.Contains(items, item => item.Contains("Riley Rivera") || item.Contains("Nora Newcomer"));
        Assert.Contains(CfUserMenu.ViewPublicSite, items);
        Assert.Contains(CfUserMenu.SignOutText, items);
        Assert.Equal(showsOrganization, items.Contains(CfUserMenu.OrganizationText));
        Assert.Equal(showsOperator, items.Contains(CfUserMenu.OperatorText));
        Assert.True(items.IndexOf(CfUserMenu.ViewPublicSite) < items.IndexOf(CfUserMenu.SignOutText));
    }

    [Fact]
    public void The_header_shows_the_name_and_email()
    {
        RenderMenu(TestAccess.Member(OrganizationRole.Editor));

        var header = Popovers.Find("[data-testid=user-menu-header]");
        Assert.Contains("Riley Rivera", header.TextContent);
        Assert.Contains("riley@club.test", header.TextContent);
    }

    [Fact]
    public void The_button_shows_initials_and_is_labeled_with_the_name()
    {
        var cut = RenderMenu(TestAccess.Member(OrganizationRole.Editor), open: false);

        var avatar = cut.Find(".mud-avatar");
        Assert.Equal("RR", avatar.TextContent.Trim());
        Assert.Equal("Riley Rivera", avatar.GetAttribute("aria-label"));
    }

    [Fact]
    public void View_public_site_goes_home_and_sign_out_goes_to_the_api()
    {
        RenderMenu(TestAccess.Member(OrganizationRole.Owner, isOperator: true));

        Assert.Equal("/", Popovers.Find("[data-testid=view-public-site]").GetAttribute("href"));
        Assert.Equal(TestAccess.Logout, Popovers.Find("[data-testid=sign-out]").GetAttribute("href"));
        Assert.Equal("/organization", Popovers.Find("[data-testid=organization-link]").GetAttribute("href"));
        Assert.Equal("/operator/requests", Popovers.Find("[data-testid=operator-link]").GetAttribute("href"));
    }

    private IRenderedComponent<CfUserMenu> RenderMenu(MeResponse me, bool open = true)
    {
        SignInAs(me);
        var cut = Render<CfUserMenu>();
        if (open)
        {
            cut.OpenUserMenu();
            Popovers.WaitForAssertion(() => Assert.NotEmpty(Popovers.FindAll(".mud-menu-item")));
        }

        return cut;
    }

    private static MeResponse Person(string who) => who switch
    {
        "owner" => TestAccess.Member(OrganizationRole.Owner),
        "editor" => TestAccess.Member(OrganizationRole.Editor),
        "viewer" => TestAccess.Member(OrganizationRole.Viewer),
        "operator-member" => TestAccess.Member(OrganizationRole.Editor, isOperator: true),
        _ => TestAccess.NoMembership(isOperator: true),
    };
}
