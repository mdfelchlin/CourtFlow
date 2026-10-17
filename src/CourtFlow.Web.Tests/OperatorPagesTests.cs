using Bunit;
using CourtFlow.Domain;
using CourtFlow.Web.Components;
using CourtFlow.Web.Pages;
using CourtFlow.Web.Services;
using MudBlazor;

namespace CourtFlow.Web.Tests;

public class OperatorPagesTests : AccessTestContext
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private static readonly AccessRequestResponse Newer =
        new(Guid.NewGuid(), "Nina Newer", "nina@club.test", "Newer VBC", "", Now.AddHours(-1), Now.AddDays(7));

    private static readonly AccessRequestResponse Older =
        new(Guid.NewGuid(), "Oscar Older", "oscar@club.test", "Older VBC", "Hi", Now.AddDays(-3), Now.AddDays(4));

    private static readonly OperatorOrganizationResponse Active =
        new(Guid.NewGuid(), "Rapids VBC", OrganizationStatus.Active, 3, ["Olivia Owner"]);

    private static readonly OperatorOrganizationResponse Deactivated =
        new(Guid.NewGuid(), "Closed VBC", OrganizationStatus.Deactivated, 1, ["Carl Closed"]);

    public OperatorPagesTests()
    {
        SignInAs(TestAccess.NoMembership(isOperator: true));
        Api.AccessRequests.AddRange([Newer, Older]);
        Api.Organizations.AddRange([Active, Deactivated]);
    }

    [Fact]
    public void Requests_are_listed_oldest_first()
    {
        var cut = RenderRequests();

        var markup = cut.Find("[data-testid=requests]").TextContent;
        Assert.True(markup.IndexOf("Oscar Older", StringComparison.Ordinal) < markup.IndexOf("Nina Newer", StringComparison.Ordinal));
    }

    [Fact]
    public void Approving_into_a_new_organization_sends_its_name()
    {
        var cut = RenderRequests();

        cut.Find($"#approve-{Older.Id}").Click();
        Dialogs.WaitForAssertion(() => Assert.NotNull(Dialogs.Find("[data-testid=approve-organization-name] input")));
        Assert.Equal("Older VBC", Dialogs.Find("[data-testid=approve-organization-name] input").GetAttribute("value"));
        Dialogs.Find("[data-testid=approve-organization-name] input").Change("  Older Volleyball  ");
        Dialogs.Find("#confirm-approve").Click();

        cut.WaitForAssertion(() => Assert.Contains($"ApproveAccessRequest({Older.Id})", Api.AccessCalls));
        Assert.Equal(new ApproveAccessRequestRequest("Older Volleyball", null, null), Api.LastApproval);
    }

    [Fact]
    public void Approving_into_an_existing_organization_sends_it_and_the_role()
    {
        var cut = RenderRequests();

        cut.Find($"#approve-{Newer.Id}").Click();
        Dialogs.WaitForAssertion(() => Assert.NotNull(Dialogs.Find("[data-testid=approve-choice]")));
        var choice = Dialogs.FindComponent<MudRadioGroup<bool>>();
        Dialogs.InvokeAsync(() => choice.Instance.ValueChanged.InvokeAsync(false));
        Dialogs.WaitForAssertion(() => Assert.NotNull(Dialogs.Find("[data-testid=approve-organization]")));

        var organization = Dialogs.FindComponent<CfSelect<OperatorOrganizationResponse>>();
        Assert.Equal([Active], organization.Instance.Items);
        Dialogs.InvokeAsync(() => organization.Instance.ValueChanged.InvokeAsync(Active));
        var role = Dialogs.FindComponent<CfSelect<OrganizationRole>>();
        Dialogs.InvokeAsync(() => role.Instance.ValueChanged.InvokeAsync(OrganizationRole.Viewer));
        Dialogs.Find("#confirm-approve").Click();

        cut.WaitForAssertion(() => Assert.Contains($"ApproveAccessRequest({Newer.Id})", Api.AccessCalls));
        Assert.Equal(new ApproveAccessRequestRequest(null, Active.Id, OrganizationRole.Viewer), Api.LastApproval);
    }

    [Fact]
    public void Decline_confirms_first()
    {
        var cut = RenderRequests();

        cut.Find($"#decline-{Older.Id}").Click();
        Assert.Empty(Api.AccessCalls);
        Confirm("Decline");

        cut.WaitForAssertion(() => Assert.Contains($"DeclineAccessRequest({Older.Id})", Api.AccessCalls));
    }

    [Fact]
    public void Organizations_show_status_members_and_owners()
    {
        var cut = RenderOrganizations();

        var table = cut.Find("[data-testid=organizations]").TextContent;
        Assert.Contains("Rapids VBC", table);
        Assert.Contains("Active", table);
        Assert.Contains("Deactivated", table);
        Assert.Contains("Olivia Owner", table);
    }

    [Fact]
    public void Deactivate_names_the_organization_and_confirms()
    {
        var cut = RenderOrganizations();

        cut.Find($"#deactivate-{Active.Id}").Click();
        Dialogs.WaitForAssertion(() => Assert.Contains("Deactivate Rapids VBC?", Dialogs.Markup));
        Assert.Empty(Api.AccessCalls);
        Confirm("Deactivate");

        cut.WaitForAssertion(() => Assert.Contains($"DeactivateOrganization({Active.Id})", Api.AccessCalls));
    }

    [Fact]
    public void Reactivate_confirms_then_calls_the_api()
    {
        var cut = RenderOrganizations();

        cut.Find($"#reactivate-{Deactivated.Id}").Click();
        Confirm("Reactivate");

        cut.WaitForAssertion(() => Assert.Contains($"ReactivateOrganization({Deactivated.Id})", Api.AccessCalls));
    }

    [Fact]
    public void Invite_owner_takes_an_email()
    {
        var cut = RenderOrganizations();

        cut.Find($"#invite-owner-{Active.Id}").Click();
        Dialogs.WaitForAssertion(() => Assert.NotNull(Dialogs.Find("[data-testid=invite-email] input")));
        Assert.Empty(Dialogs.FindAll("[data-testid=invite-role]"));
        Dialogs.Find("[data-testid=invite-email] input").Change("boss@club.test");
        Dialogs.Find("#send-invite").Click();

        cut.WaitForAssertion(() => Assert.Contains($"InviteOwner({Active.Id},boss@club.test)", Api.AccessCalls));
    }

    private IRenderedComponent<OperatorRequests> RenderRequests()
    {
        var cut = Render<OperatorRequests>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=requests]")));
        return cut;
    }

    private IRenderedComponent<OperatorOrganizations> RenderOrganizations()
    {
        var cut = Render<OperatorOrganizations>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=organizations]")));
        return cut;
    }
}
