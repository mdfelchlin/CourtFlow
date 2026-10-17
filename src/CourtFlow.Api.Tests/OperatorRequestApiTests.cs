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
public sealed class OperatorRequestApiTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Only_operators_can_use_it()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/operator/requests", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(AccessReasons.Forbidden, await ReasonAsync(await factory.CreateMemberClient().GetAsync("/api/operator/requests", TestContext.Current.CancellationToken)));
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateOperatorClient().GetAsync("/api/operator/requests", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Waiting_requests_are_listed_oldest_first()
    {
        await AccessRequestApiTests.SubmitAsync(factory.CreateClient(), AccessRequestApiTests.Form("first@club.test"));
        factory.Clock.Advance(TimeSpan.FromHours(1));
        await AccessRequestApiTests.SubmitAsync(factory.CreateClient(), AccessRequestApiTests.Form("second@club.test"));

        var list = await ListAsync();

        Assert.Equal(["first@club.test", "second@club.test"], list.Select(item => item.Email));
    }

    [Fact]
    public async Task Approving_into_a_new_organization_creates_it_invites_the_owner_and_sends_email_3()
    {
        var request = await SubmitAsync();
        factory.Email.Reset();

        var response = await PostAsync(factory.CreateOperatorClient(), $"/api/operator/requests/{request.Id}/approve",
            new ApproveAccessRequest("River City VBC", null, null));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var organization = await factory.WithDbAsync(db => db.Organizations.AsNoTracking().SingleAsync(item => item.Name == "River City VBC"));
        var invitation = await factory.WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync());
        Assert.Equal(organization.Id, invitation.OrganizationId);
        Assert.Equal(OrganizationRole.Owner, invitation.Role);
        Assert.Equal("operator@courtflow.test", invitation.SentByOperatorEmail);
        Assert.Equal([request.WaitlistEntryId], factory.WorkOs.Approved);
        Assert.Contains((await factory.OutboxAsync()), email => email.Subject == "Your CourtFlow account is ready" && email.To.SequenceEqual(["casey@club.test"]));
        Assert.Empty(await ListAsync());
    }

    [Fact]
    public async Task Approving_into_an_existing_organization_uses_the_role_and_sends_email_4()
    {
        var request = await SubmitAsync();

        var response = await PostAsync(factory.CreateOperatorClient(), $"/api/operator/requests/{request.Id}/approve",
            new ApproveAccessRequest(null, TestOrganizationSeed.Id, OrganizationRole.Viewer));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var invitation = await factory.WithDbAsync(db => db.Invitations.AsNoTracking().SingleAsync());
        Assert.Equal(TestOrganizationSeed.Id, invitation.OrganizationId);
        Assert.Equal(OrganizationRole.Viewer, invitation.Role);
        var email = (await factory.OutboxAsync()).Single(item => item.Subject == "You've been added to Test Organization on CourtFlow");
        Assert.Contains("as a viewer", email.Text);
    }

    [Fact]
    public async Task Approving_someone_who_already_has_access_is_refused()
    {
        var request = await SubmitAsync();
        factory.AddMember(TestOrganizationSeed.Id, new TestUser("user_casey", "casey@club.test", "Casey"), OrganizationRole.Viewer);

        var response = await Approve(request.Id);

        Assert.Equal(AccessRequestUseCases.RequesterHasAccess, await ErrorAsync(response));
    }

    [Fact]
    public async Task Approving_someone_with_a_pending_invitation_is_refused()
    {
        var request = await SubmitAsync();
        (await PostAsync(factory.CreateMemberClient(), "/api/organization/invitations", new InviteMember("casey@club.test", OrganizationRole.Editor))).EnsureSuccessStatusCode();

        var response = await Approve(request.Id);

        Assert.Equal(AccessRequestUseCases.RequesterHasInvitation, await ErrorAsync(response));
    }

    [Fact]
    public async Task Approving_into_a_deactivated_organization_is_refused()
    {
        var request = await SubmitAsync();
        var gone = await factory.CreateOrganizationAsync("Gone Club", OrganizationStatus.Deactivated);

        var response = await PostAsync(factory.CreateOperatorClient(), $"/api/operator/requests/{request.Id}/approve",
            new ApproveAccessRequest(null, gone.Id, OrganizationRole.Editor));

        Assert.Equal(InvitationRules.Deactivated, await ErrorAsync(response));
    }

    [Fact]
    public async Task Declining_denies_the_waitlist_entry_and_sends_email_5()
    {
        var request = await SubmitAsync();

        var response = await PostAsync(factory.CreateOperatorClient(), $"/api/operator/requests/{request.Id}/decline");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([request.WaitlistEntryId], factory.WorkOs.Denied);
        Assert.Contains(await factory.OutboxAsync(), email => email.Subject == "Your CourtFlow access request");
    }

    private async Task<AccessRequest> SubmitAsync()
    {
        await AccessRequestApiTests.SubmitAsync(factory.CreateClient(), AccessRequestApiTests.Form());
        return await factory.WithDbAsync(db => db.AccessRequests.AsNoTracking().SingleAsync());
    }

    private Task<HttpResponseMessage> Approve(Guid id) =>
        PostAsync(factory.CreateOperatorClient(), $"/api/operator/requests/{id}/approve", new ApproveAccessRequest("River City VBC", null, null));

    private async Task<List<AccessRequestDetails>> ListAsync() =>
        (await factory.CreateOperatorClient().GetFromJsonAsync<List<AccessRequestDetails>>("/api/operator/requests", TestContext.Current.CancellationToken))!;
}
