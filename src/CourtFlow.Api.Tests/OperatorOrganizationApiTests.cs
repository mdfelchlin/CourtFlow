using System.Net;
using System.Net.Http.Json;
using CourtFlow.Api.Access;
using CourtFlow.Application.Access;
using CourtFlow.Application.Organizations;
using CourtFlow.Domain;
using CourtFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static CourtFlow.Api.Tests.AccessTestHelpers;

namespace CourtFlow.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class OperatorOrganizationApiTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Only_operators_can_use_it()
    {
        Assert.Equal(AccessReasons.Forbidden, await ReasonAsync(await factory.CreateMemberClient().GetAsync("/api/operator/organizations", TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task The_list_shows_status_member_count_and_owners()
    {
        factory.AddMember(TestOrganizationSeed.Id, TestUser.Named("zed"), OrganizationRole.Owner);
        factory.AddMember(TestOrganizationSeed.Id, TestUser.Named("amy"), OrganizationRole.Owner);
        factory.AddMember(TestOrganizationSeed.Id, TestUser.Named("ed"), OrganizationRole.Editor);
        await factory.CreateOrganizationAsync("Alpha Club", OrganizationStatus.Deactivated);

        var list = await ListAsync();

        Assert.Equal(["Alpha Club", "Test Organization"], list.Select(item => item.Name));
        Assert.Equal(OrganizationStatus.Deactivated, list[0].Status);
        Assert.Equal(0, list[0].MemberCount);
        Assert.Empty(list[0].OwnerNames);
        Assert.Equal(OrganizationStatus.Active, list[1].Status);
        Assert.Equal(3, list[1].MemberCount);
        Assert.Equal(["Test amy", "Test zed"], list[1].OwnerNames);
    }

    [Fact]
    public async Task Inviting_an_owner_sends_email_7()
    {
        var response = await PostAsync(factory.CreateOperatorClient(), $"/api/operator/organizations/{TestOrganizationSeed.Id}/owner-invitations", new InviteOwner("New.Owner@club.test"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["new.owner@club.test"], factory.WorkOs.InvitationsSentTo);
        var email = Assert.Single(await factory.OutboxAsync());
        Assert.Equal("You're the owner of Test Organization on CourtFlow", email.Subject);
        var invitation = await factory.WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync());
        Assert.Equal(OrganizationRole.Owner, invitation.Role);
    }

    [Fact]
    public async Task Inviting_an_owner_has_the_same_refusals()
    {
        factory.AddMember(TestOrganizationSeed.Id, TestUser.Named("taken"), OrganizationRole.Viewer);

        var response = await PostAsync(factory.CreateOperatorClient(), $"/api/operator/organizations/{TestOrganizationSeed.Id}/owner-invitations", new InviteOwner("taken@courtflow.test"));

        Assert.Equal(InvitationRules.AlreadyMember, await ErrorAsync(response));
    }

    [Fact]
    public async Task Deactivating_cancels_and_revokes_invitations_emails_members_and_ends_access()
    {
        var owner = factory.CreateMemberClient();
        factory.AddMember(TestOrganizationSeed.Id, TestUser.Named("ed"), OrganizationRole.Editor);
        (await PostAsync(owner, "/api/organization/invitations", new InviteMember("pending@club.test", OrganizationRole.Viewer))).EnsureSuccessStatusCode();
        var invitation = await factory.WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync());
        factory.Email.Reset();

        var response = await PostAsync(factory.CreateOperatorClient(), $"/api/operator/organizations/{TestOrganizationSeed.Id}/deactivate");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(InvitationState.Cancelled, (await factory.WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync())).State);
        Assert.Equal([invitation.WorkOsInvitationId], factory.WorkOs.Revoked);
        var deactivated = (await factory.OutboxAsync()).Where(email => email.Subject == "Test Organization has been deactivated on CourtFlow").ToList();
        Assert.Equal(["ed@courtflow.test", "member-owner@courtflow.test"], deactivated.SelectMany(email => email.To).Order());
        Assert.Equal(AccessReasons.OrganizationDeactivated, await ReasonAsync(await owner.GetAsync("/api/tournaments", TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Reactivating_sends_email_9_and_restores_access_but_not_invitations()
    {
        var owner = factory.CreateMemberClient();
        (await PostAsync(owner, "/api/organization/invitations", new InviteMember("pending@club.test", OrganizationRole.Viewer))).EnsureSuccessStatusCode();
        (await PostAsync(factory.CreateOperatorClient(), $"/api/operator/organizations/{TestOrganizationSeed.Id}/deactivate")).EnsureSuccessStatusCode();

        var response = await PostAsync(factory.CreateOperatorClient(), $"/api/operator/organizations/{TestOrganizationSeed.Id}/reactivate");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(await factory.OutboxAsync(), email => email.Subject == "Test Organization is active again on CourtFlow");
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/tournaments", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(InvitationState.Cancelled, (await factory.WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync())).State);
    }

    private async Task<List<OperatorOrganizationDetails>> ListAsync() =>
        (await factory.CreateOperatorClient().GetFromJsonAsync<List<OperatorOrganizationDetails>>("/api/operator/organizations", TestContext.Current.CancellationToken))!;
}
