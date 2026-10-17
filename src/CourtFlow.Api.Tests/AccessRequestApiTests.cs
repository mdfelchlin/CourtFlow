using System.Net.Http.Json;
using CourtFlow.Application.Access;
using CourtFlow.Domain;
using CourtFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static CourtFlow.Api.Tests.AccessTestHelpers;

namespace CourtFlow.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class AccessRequestApiTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    internal static SubmitAccessRequest Form(string email = "casey@club.test", string? message = "We run 20 events a year.") =>
        new("Casey Jones", email, "River City VBC", message, "token-1");

    internal static async Task<AccessRequestResult> SubmitAsync(HttpClient client, SubmitAccessRequest form)
    {
        var response = await client.PostAsJsonAsync("/api/access-requests", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccessRequestResult>())!;
    }

    [Fact]
    public async Task A_failed_check_stops_before_anything_else()
    {
        factory.Captcha.Passes = false;

        var result = await SubmitAsync(factory.CreateClient(), Form());

        Assert.Equal(AccessRequestOutcome.CaptchaFailed, result.Outcome);
        Assert.Equal("token-1", Assert.Single(factory.Captcha.Tokens));
        Assert.Empty(factory.WorkOs.Entries);
    }

    [Fact]
    public async Task Someone_with_access_is_told_to_sign_in()
    {
        factory.AddMember(TestOrganizationSeed.Id, new TestUser("user_casey", "casey@club.test", "Casey"), OrganizationRole.Viewer);

        var result = await SubmitAsync(factory.CreateClient(), Form("CASEY@club.test"));

        Assert.Equal(new AccessRequestResult(AccessRequestOutcome.AlreadyHasAccess, AccessRequestUseCases.AlreadyHasAccessMessage), result);
    }

    [Fact]
    public async Task A_waiting_request_is_reported()
    {
        await SubmitAsync(factory.CreateClient(), Form());

        var result = await SubmitAsync(factory.CreateClient(), Form());

        Assert.Equal(new AccessRequestResult(AccessRequestOutcome.AlreadyWaiting, AccessRequestUseCases.AlreadyWaitingMessage), result);
        Assert.Single(factory.WorkOs.Entries);
    }

    [Fact]
    public async Task A_declined_email_cannot_send_again()
    {
        await SubmitAsync(factory.CreateClient(), Form());
        var waiting = (await factory.CreateOperatorClient().GetFromJsonAsync<List<AccessRequestDetails>>("/api/operator/requests", TestContext.Current.CancellationToken))!.Single();
        (await PostAsync(factory.CreateOperatorClient(), $"/api/operator/requests/{waiting.Id}/decline")).EnsureSuccessStatusCode();

        var result = await SubmitAsync(factory.CreateClient(), Form());

        Assert.Equal(new AccessRequestResult(AccessRequestOutcome.Declined, AccessRequestUseCases.DeclinedMessage), result);
    }

    [Fact]
    public async Task A_good_request_creates_the_waitlist_entry_stores_it_and_emails_every_operator()
    {
        var result = await SubmitAsync(factory.CreateClient(), Form());

        Assert.Equal(new AccessRequestResult(AccessRequestOutcome.Sent, AccessRequestUseCases.SentMessage), result);
        var entry = Assert.Single(factory.WorkOs.Entries);
        Assert.Equal("casey@club.test", entry.Email);
        Assert.Equal("Casey Jones", entry.Fields["name"]);
        Assert.Equal("River City VBC", entry.Fields["organization_name"]);
        Assert.Equal("We run 20 events a year.", entry.Fields["message"]);

        var stored = await factory.WithDbAsync(db => db.AccessRequests.AsNoTracking().SingleAsync());
        Assert.Equal(AccessRequestState.Waiting, stored.State);
        Assert.Equal(entry.Id, stored.WaitlistEntryId);

        var email = Assert.Single(await factory.OutboxAsync());
        Assert.Equal([ApiFactory.OperatorEmail, ApiFactory.SecondOperatorEmail], email.To);
        Assert.Equal("CourtFlow access request from Casey Jones", email.Subject);
    }

    [Fact]
    public async Task When_WorkOS_fails_nothing_is_stored_or_sent()
    {
        factory.WorkOs.Unavailable = true;

        var result = await SubmitAsync(factory.CreateClient(), Form());

        Assert.Equal(AccessRequestOutcome.CouldNotSend, result.Outcome);
        Assert.Equal(0, await factory.WithDbAsync(db => db.AccessRequests.CountAsync()));
        Assert.Empty(await factory.OutboxAsync());
    }

    [Fact]
    public async Task The_signed_in_email_wins_over_the_form()
    {
        var client = factory.CreateClientAs(new TestUser("user_signed", "signed@club.test", "Signed In"));

        await SubmitAsync(client, Form("someone.else@club.test"));

        Assert.Equal("signed@club.test", Assert.Single(factory.WorkOs.Entries).Email);
    }

    [Fact]
    public async Task Field_problems_are_refused()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/access-requests", Form() with { Name = " " }, TestContext.Current.CancellationToken);

        Assert.Equal(AccessRequest.NameRequired, await ErrorAsync(response));
    }
}
