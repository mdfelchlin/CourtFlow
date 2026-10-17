using CourtFlow.Application.Access;
using CourtFlow.Domain;
using Microsoft.EntityFrameworkCore;
using static CourtFlow.Api.Tests.AccessTestHelpers;

namespace CourtFlow.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class AccessRequestSweepTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Reminder = "Your CourtFlow access request is still waiting";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task The_reminder_is_sent_once_in_the_last_24_hours()
    {
        await SubmitAsync();

        factory.Clock.Advance(TimeSpan.FromDays(6) - TimeSpan.FromMinutes(1));
        await factory.RunAccessRequestSweepAsync();
        Assert.Equal(0, await RemindersAsync());

        factory.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(new AccessRequestSweep.Result(1, 0), await factory.RunAccessRequestSweepAsync());
        factory.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(new AccessRequestSweep.Result(0, 0), await factory.RunAccessRequestSweepAsync());
        Assert.Equal(1, await RemindersAsync());
    }

    [Fact]
    public async Task No_reminder_after_approval()
    {
        var request = await SubmitAsync();
        (await PostAsync(factory.CreateOperatorClient(), $"/api/operator/requests/{request.Id}/approve",
            new ApproveAccessRequest("River City VBC", null, null))).EnsureSuccessStatusCode();

        factory.Clock.Advance(TimeSpan.FromDays(6.5));
        await factory.RunAccessRequestSweepAsync();

        Assert.Equal(0, await RemindersAsync());
    }

    [Fact]
    public async Task Expiry_at_7_days_deletes_the_waitlist_entry()
    {
        var request = await SubmitAsync();

        factory.Clock.Advance(TimeSpan.FromDays(7));
        var result = await factory.RunAccessRequestSweepAsync();

        Assert.Equal(1, result.Expired);
        Assert.Equal([request.WaitlistEntryId], factory.WorkOs.Deleted);
        Assert.Equal(AccessRequestState.Expired, (await StoredAsync(request.Id)).State);
    }

    [Fact]
    public async Task The_same_email_can_send_again_after_expiry()
    {
        await SubmitAsync();
        factory.Clock.Advance(TimeSpan.FromDays(7));
        await factory.RunAccessRequestSweepAsync();

        var result = await AccessRequestApiTests.SubmitAsync(factory.CreateClient(), AccessRequestApiTests.Form());

        Assert.Equal(AccessRequestOutcome.Sent, result.Outcome);
    }

    [Fact]
    public async Task A_missing_waitlist_entry_marks_the_request_expired()
    {
        var request = await SubmitAsync();
        factory.WorkOs.MissingEntries.Add(request.WaitlistEntryId);

        factory.Clock.Advance(TimeSpan.FromHours(1));
        await factory.RunAccessRequestSweepAsync();

        Assert.Equal(AccessRequestState.Expired, (await StoredAsync(request.Id)).State);
        Assert.Empty(factory.WorkOs.Deleted);
    }

    private async Task<AccessRequest> SubmitAsync()
    {
        await AccessRequestApiTests.SubmitAsync(factory.CreateClient(), AccessRequestApiTests.Form());
        return await factory.WithDbAsync(db => db.AccessRequests.AsNoTracking().SingleAsync(item => item.State == AccessRequestState.Waiting));
    }

    private Task<AccessRequest> StoredAsync(Guid id) =>
        factory.WithDbAsync(db => db.AccessRequests.AsNoTracking().SingleAsync(item => item.Id == id));

    private async Task<int> RemindersAsync() => (await factory.OutboxAsync()).Count(email => email.Subject == Reminder);
}
