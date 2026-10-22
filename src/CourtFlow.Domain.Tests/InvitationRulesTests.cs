using CourtFlow.Domain;

namespace CourtFlow.Domain.Tests;

public class InvitationRulesTests
{
    private static readonly DateTimeOffset Sent = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheEmailIsTrimmedAndLowercased()
    {
        var invitation = Create(email: "  Pat@Example.COM ");

        Assert.Equal("pat@example.com", invitation.Email);
    }

    [Fact]
    public void ABadEmailIsRefused()
    {
        Assert.Throws<DomainRuleException>(() => Create(email: "not-an-email"));
    }

    [Fact]
    public void ItExpiresExactly7DaysAfterItWasSent()
    {
        var invitation = Create();

        Assert.Equal(Sent.AddDays(7), invitation.ExpiresAt);
        Assert.False(invitation.IsExpired(Sent.AddDays(7).AddTicks(-1)));
        Assert.True(invitation.IsOpen(Sent.AddDays(7).AddTicks(-1)));
        Assert.True(invitation.IsExpired(Sent.AddDays(7)));
        Assert.False(invitation.IsOpen(Sent.AddDays(7)));
    }

    [Fact]
    public void AcceptingAPendingInvitationMarksItAccepted()
    {
        var invitation = Create();

        invitation.Accept(Sent.AddDays(1));

        Assert.Equal(InvitationState.Accepted, invitation.State);
    }

    [Fact]
    public void AnExpiredInvitationCannotBeAccepted()
    {
        var invitation = Create();

        var error = Assert.Throws<DomainRuleException>(() => invitation.Accept(Sent.AddDays(7)));

        Assert.Equal(Invitation.Expired, error.Message);
    }

    [Fact]
    public void ACancelledOrAcceptedInvitationCannotBeAccepted()
    {
        var cancelled = Create();
        cancelled.Cancel();
        var accepted = Create();
        accepted.Accept(Sent);

        Assert.Equal(Invitation.NotPending, Assert.Throws<DomainRuleException>(() => cancelled.Accept(Sent)).Message);
        Assert.Equal(Invitation.NotPending, Assert.Throws<DomainRuleException>(() => accepted.Accept(Sent)).Message);
    }

    [Fact]
    public void ReissueGivesAFreshLinkAndAFresh7Days()
    {
        var invitation = Create();
        var later = Sent.AddDays(10);

        invitation.Reissue("inv_new", later);

        Assert.Equal("inv_new", invitation.WorkOsInvitationId);
        Assert.Equal(later, invitation.SentAt);
        Assert.Equal(later.AddDays(7), invitation.ExpiresAt);
        Assert.True(invitation.IsOpen(later));
    }

    [Fact]
    public void ReissueBringsBackACancelledInvitation()
    {
        var invitation = Create();
        invitation.Cancel();

        invitation.Reissue("inv_new", Sent.AddDays(1));

        Assert.Equal(InvitationState.Pending, invitation.State);
    }

    [Fact]
    public void AnAcceptedInvitationCannotBeReissuedOrCancelled()
    {
        var invitation = Create();
        invitation.Accept(Sent);

        Assert.Throws<DomainRuleException>(() => invitation.Reissue("inv_new", Sent));
        Assert.Throws<DomainRuleException>(invitation.Cancel);
    }

    private static Invitation Create(string email = "pat@example.com") =>
        Invitation.Create(Guid.NewGuid(), email, OrganizationRole.Editor, "user_owner", null, "inv_1", Sent);
}