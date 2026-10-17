using System.Net;
using System.Net.Http.Json;
using CourtFlow.Api.Access;
using CourtFlow.Application.Access;
using CourtFlow.Domain;
using CourtFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static CourtFlow.Api.Tests.AccessTestHelpers;

namespace CourtFlow.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class OrganizationMembersApiTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [InlineData(OrganizationRole.Editor)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Only_owners_can_use_it(OrganizationRole role)
    {
        var response = await factory.CreateMemberClient(role).GetAsync("/api/organization", TestContext.Current.CancellationToken);

        Assert.Equal(AccessReasons.Forbidden, await ReasonAsync(response));
    }

    [Fact]
    public async Task The_overview_lists_members_and_pending_invitations()
    {
        var owner = factory.CreateMemberClient();
        factory.AddMember(TestOrganizationSeed.Id, TestUser.Named("ed"), OrganizationRole.Editor);
        await InviteAsync(owner, "pending@club.test", OrganizationRole.Viewer);

        var overview = await OverviewAsync(owner);

        Assert.Equal(["Test ed", "Test member-owner"], overview.Members.Select(member => member.Name));
        var me = overview.Members.Single(member => member.IsYou);
        Assert.True(me.IsLastOwner);
        var invitation = Assert.Single(overview.Invitations);
        Assert.Equal("pending@club.test", invitation.Email);
        Assert.Equal(TestClock.Start.AddDays(7), invitation.ExpiresAt);
    }

    [Fact]
    public async Task Inviting_creates_a_WorkOS_invitation_and_sends_email_6()
    {
        var owner = factory.CreateMemberClient();

        var response = await InviteAsync(owner, "New@Club.test", OrganizationRole.Editor);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["new@club.test"], factory.WorkOs.InvitationsSentTo);
        var email = Assert.Single(await factory.OutboxAsync());
        Assert.Equal("Test member-owner invited you to Test Organization on CourtFlow", email.Subject);
        Assert.Contains("as an editor", email.Text);
    }

    [Fact]
    public async Task Inviting_refuses_each_of_the_four_cases()
    {
        var owner = factory.CreateMemberClient();
        factory.AddMember(TestOrganizationSeed.Id, TestUser.Named("here"), OrganizationRole.Viewer);
        var other = await factory.CreateOrganizationAsync("Other Club");
        factory.AddMember(other.Id, TestUser.Named("there"), OrganizationRole.Viewer);
        var otherOwner = TestUser.Named("other-owner");
        factory.AddMember(other.Id, otherOwner, OrganizationRole.Owner);
        await InviteAsync(owner, "invited.here@club.test", OrganizationRole.Viewer);
        await InviteAsync(factory.CreateClientAs(otherOwner), "invited.there@club.test", OrganizationRole.Viewer);

        Assert.Equal(Application.Organizations.InvitationRules.AlreadyMember, await ErrorAsync(await InviteAsync(owner, "here@courtflow.test", OrganizationRole.Viewer)));
        Assert.Equal(Application.Organizations.InvitationRules.BelongsElsewhere, await ErrorAsync(await InviteAsync(owner, "there@courtflow.test", OrganizationRole.Viewer)));
        Assert.Equal(Application.Organizations.InvitationRules.AlreadyInvitedHere, await ErrorAsync(await InviteAsync(owner, "invited.here@club.test", OrganizationRole.Viewer)));
        Assert.Equal(Application.Organizations.InvitationRules.InvitedElsewhere, await ErrorAsync(await InviteAsync(owner, "invited.there@club.test", OrganizationRole.Viewer)));
    }

    [Fact]
    public async Task Resending_revokes_the_old_link_and_gives_a_fresh_7_days_even_when_expired()
    {
        var owner = factory.CreateMemberClient();
        await InviteAsync(owner, "pending@club.test", OrganizationRole.Viewer);
        var first = await factory.WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync());
        factory.Clock.Advance(TimeSpan.FromDays(8));

        var response = await PostAsync(owner, $"/api/organization/invitations/{first.Id}/resend");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([first.WorkOsInvitationId], factory.WorkOs.Revoked);
        var again = await factory.WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync());
        Assert.NotEqual(first.WorkOsInvitationId, again.WorkOsInvitationId);
        Assert.Equal(factory.Clock.GetUtcNow().AddDays(7), again.ExpiresAt);
        Assert.Equal(2, (await factory.OutboxAsync()).Count);
    }

    [Fact]
    public async Task Cancelling_revokes_the_invitation()
    {
        var owner = factory.CreateMemberClient();
        await InviteAsync(owner, "pending@club.test", OrganizationRole.Viewer);
        var invitation = await factory.WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync());

        var response = await owner.DeleteAsync($"/api/organization/invitations/{invitation.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([invitation.WorkOsInvitationId], factory.WorkOs.Revoked);
        Assert.Empty((await OverviewAsync(owner)).Invitations);
    }

    [Fact]
    public async Task Another_organizations_invitation_is_not_found()
    {
        var other = await factory.CreateOrganizationAsync("Other Club");
        var otherOwner = TestUser.Named("other-owner");
        factory.AddMember(other.Id, otherOwner, OrganizationRole.Owner);
        await InviteAsync(factory.CreateClientAs(otherOwner), "pending@club.test", OrganizationRole.Viewer);
        var invitation = await factory.WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync());

        var response = await factory.CreateMemberClient().DeleteAsync($"/api/organization/invitations/{invitation.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_owner_changes_a_role_and_removes_a_member()
    {
        var owner = factory.CreateMemberClient();
        factory.AddMember(TestOrganizationSeed.Id, TestUser.Named("ed"), OrganizationRole.Editor);
        var ed = (await OverviewAsync(owner)).Members.Single(member => member.Name == "Test ed");

        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsJsonAsync($"/api/organization/members/{ed.Id}/role", new ChangeMemberRole(OrganizationRole.Viewer), TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(OrganizationRole.Viewer, (await OverviewAsync(owner)).Members.Single(member => member.Id == ed.Id).Role);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/organization/members/{ed.Id}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.DoesNotContain((await OverviewAsync(owner)).Members, member => member.Id == ed.Id);
    }

    [Fact]
    public async Task The_last_owner_cannot_be_demoted_or_removed()
    {
        var owner = factory.CreateMemberClient();
        var me = (await OverviewAsync(owner)).Members.Single(member => member.IsYou);

        Assert.Equal(Organization.NeedsAnOwner, await ErrorAsync(await owner.PutAsJsonAsync($"/api/organization/members/{me.Id}/role", new ChangeMemberRole(OrganizationRole.Editor), TestContext.Current.CancellationToken)));
        Assert.Equal(Organization.NeedsAnOwner, await ErrorAsync(await owner.DeleteAsync($"/api/organization/members/{me.Id}", TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Removing_yourself_leaves_your_next_call_without_access()
    {
        var owner = factory.CreateMemberClient();
        factory.AddMember(TestOrganizationSeed.Id, TestUser.Named("second"), OrganizationRole.Owner);
        var me = (await OverviewAsync(owner)).Members.Single(member => member.IsYou);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/organization/members/{me.Id}", TestContext.Current.CancellationToken)).StatusCode);

        Assert.Equal(AccessReasons.NoAccess, await ReasonAsync(await owner.GetAsync("/api/organization", TestContext.Current.CancellationToken)));
    }

    private static Task<HttpResponseMessage> InviteAsync(HttpClient client, string email, OrganizationRole role) =>
        PostAsync(client, "/api/organization/invitations", new InviteMember(email, role));

    private static async Task<OrganizationOverview> OverviewAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<OrganizationOverview>("/api/organization", TestContext.Current.CancellationToken))!;
}
