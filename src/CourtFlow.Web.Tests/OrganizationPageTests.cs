using Bunit;
using CourtFlow.Domain;
using CourtFlow.Web.Components;
using CourtFlow.Web.Pages;
using CourtFlow.Web.Services;
using MudBlazor;

namespace CourtFlow.Web.Tests;

public class OrganizationPageTests : AccessTestContext
{
    private static readonly MemberResponse Owner =
        new(Guid.NewGuid(), "Olivia Owner", "olivia@club.test", OrganizationRole.Owner, IsLastOwner: true, IsYou: true);

    private static readonly MemberResponse Editor =
        new(Guid.NewGuid(), "Eddie Editor", "eddie@club.test", OrganizationRole.Editor, IsLastOwner: false, IsYou: false);

    private static readonly InvitationResponse Pending = new(
        Guid.NewGuid(), "pat@club.test", OrganizationRole.Viewer,
        new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero), false);

    private static readonly InvitationResponse Expired = new(
        Guid.NewGuid(), "old@club.test", OrganizationRole.Editor,
        new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero), true);

    public OrganizationPageTests()
    {
        SignInAs(TestAccess.Member(OrganizationRole.Owner));
        Api.Organization = new OrganizationOverviewResponse(Guid.NewGuid(), "Rapids VBC", [Owner, Editor], [Pending, Expired]);
    }

    [Fact]
    public void Members_and_invitations_render_under_the_organization_name()
    {
        var cut = RenderPage();

        Assert.Equal("Rapids VBC", cut.Find("h1").TextContent.Trim());
        Assert.Contains("Olivia Owner", cut.Markup);
        Assert.Contains("eddie@club.test", cut.Markup);
        Assert.Contains("pat@club.test", cut.Markup);
        Assert.Contains(OrganizationPage.ExpiredText, cut.Find("[data-testid=invitations]").TextContent);
    }

    [Fact]
    public void The_last_owner_cannot_be_demoted_or_removed()
    {
        var cut = RenderPage();

        Assert.True(cut.Find($"#remove-{Owner.Id}").HasAttribute("disabled"));
        Assert.False(cut.Find($"#remove-{Editor.Id}").HasAttribute("disabled"));
        var roles = cut.FindComponents<MudSelect<OrganizationRole>>();
        Assert.True(roles[0].Find("input").HasAttribute("disabled"));
        Assert.False(roles[1].Find("input").HasAttribute("disabled"));
        Assert.Contains(cut.FindComponents<MudTooltip>(), tooltip => tooltip.Instance.Text == Domain.Organization.NeedsAnOwner);
    }

    [Fact]
    public void Changing_a_role_calls_the_api_at_once()
    {
        var cut = RenderPage();
        var editorRole = cut.FindComponents<MudSelect<OrganizationRole>>()[1];

        cut.InvokeAsync(() => editorRole.Instance.ValueChanged.InvokeAsync(OrganizationRole.Viewer));

        cut.WaitForAssertion(() => Assert.Contains($"ChangeMemberRole({Editor.Id},Viewer)", Api.AccessCalls));
    }

    [Fact]
    public void Remove_confirms_then_calls_the_api()
    {
        var cut = RenderPage();

        cut.Find($"#remove-{Editor.Id}").Click();
        Assert.Empty(Api.AccessCalls);
        Confirm("Remove");

        cut.WaitForAssertion(() => Assert.Contains($"RemoveMember({Editor.Id})", Api.AccessCalls));
    }

    [Fact]
    public void Cancel_confirms_then_calls_the_api()
    {
        var cut = RenderPage();

        cut.Find($"#cancel-{Pending.Id}").Click();
        Assert.Empty(Api.AccessCalls);
        Confirm("Cancel invitation");

        cut.WaitForAssertion(() => Assert.Contains($"CancelInvitation({Pending.Id})", Api.AccessCalls));
    }

    [Fact]
    public void Resend_works_on_an_expired_invitation()
    {
        var cut = RenderPage();

        cut.Find($"#resend-{Expired.Id}").Click();

        cut.WaitForAssertion(() => Assert.Contains($"ResendInvitation({Expired.Id})", Api.AccessCalls));
        Assert.True(Snackbar(OrganizationPage.Resent));
    }

    [Fact]
    public void Invite_defaults_to_editor_and_sends_the_email()
    {
        var cut = RenderPage();

        cut.Find("#invite-member").Click();
        Dialogs.WaitForAssertion(() => Assert.NotNull(Dialogs.Find("[data-testid=invite-email] input")));
        Assert.Equal(OrganizationRole.Editor, Dialogs.FindComponent<CfSelect<OrganizationRole>>().Instance.Value);
        Dialogs.Find("[data-testid=invite-email] input").Change("new@club.test");
        Dialogs.Find("#send-invite").Click();

        cut.WaitForAssertion(() => Assert.Contains("InviteMember(new@club.test,Editor)", Api.AccessCalls));
        Assert.True(Snackbar(InviteDialog.SentMessage));
    }

    [Fact]
    public void A_refused_invitation_keeps_the_dialog_open_with_the_message()
    {
        Api.RejectAccessWith = "This person already belongs to another organization.";
        var cut = RenderPage();

        cut.Find("#invite-member").Click();
        Dialogs.WaitForAssertion(() => Assert.NotNull(Dialogs.Find("[data-testid=invite-email] input")));
        Dialogs.Find("[data-testid=invite-email] input").Change("taken@club.test");
        Dialogs.Find("#send-invite").Click();

        Dialogs.WaitForAssertion(() => Assert.True(Snackbar("This person already belongs to another organization.")));
        Assert.NotNull(Dialogs.Find("#send-invite"));
    }

    private IRenderedComponent<OrganizationPage> RenderPage()
    {
        var cut = Render<OrganizationPage>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=members]")));
        return cut;
    }
}
