using System.Net;
using CourtFlow.Application.Access;
using CourtFlow.Domain;
using CourtFlow.Infrastructure;
using CourtFlow.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using static CourtFlow.Api.Tests.AccessTestHelpers;

namespace CourtFlow.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class EmailOutboxTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task An_action_and_its_email_save_together()
    {
        await InviteAsync("new@club.test");

        Assert.Single(await factory.WithDbAsync(db => db.Invitations.AsNoTracking().ToListAsync()));
        Assert.Single(await factory.OutboxAsync());
    }

    [Fact]
    public async Task A_failing_save_leaves_no_email()
    {
        await factory.WithDbAsync(async db =>
        {
            var outbox = new EmailOutbox(db, factory.Clock);
            outbox.Enqueue(new Application.Email.EmailMessage(["a@club.test"], "Subject", "Text", "<p>Text</p>"));
            db.Organizations.Add(new Organization(TestOrganizationSeed.Id, "Duplicate"));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
            return true;
        });

        Assert.Empty(await factory.OutboxAsync());
    }

    [Fact]
    public async Task Due_emails_are_sent()
    {
        await InviteAsync("new@club.test");

        Assert.Equal(1, await factory.SendDueEmailsAsync());

        var sent = Assert.Single(factory.Email.Sent);
        Assert.Equal(["new@club.test"], sent.To);
        Assert.NotNull(Assert.Single(await factory.OutboxAsync()).SentAt);
        Assert.Equal(0, await factory.SendDueEmailsAsync());
    }

    [Fact]
    public async Task A_failing_sender_retries_on_schedule_and_gives_up_after_5_retries()
    {
        await InviteAsync("new@club.test");
        factory.Email.Fails = true;

        await factory.SendDueEmailsAsync();
        foreach (var delay in EmailOutboxSender.RetryDelays)
        {
            factory.Clock.Advance(delay - TimeSpan.FromSeconds(1));
            await factory.SendDueEmailsAsync();
            var attemptsBefore = factory.Email.Attempts;

            factory.Clock.Advance(TimeSpan.FromSeconds(1));
            await factory.SendDueEmailsAsync();
            Assert.Equal(attemptsBefore + 1, factory.Email.Attempts);
        }

        factory.Clock.Advance(TimeSpan.FromHours(1));
        await factory.SendDueEmailsAsync();

        Assert.Equal(6, factory.Email.Attempts);
        var email = Assert.Single(await factory.OutboxAsync());
        Assert.Null(email.NextAttemptAt);
        Assert.Null(email.SentAt);
        Assert.NotNull(email.LastError);
    }

    [Fact]
    public async Task The_action_succeeds_whatever_the_sender_does()
    {
        factory.Email.Fails = true;

        var response = await InviteAsync("new@club.test");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private Task<HttpResponseMessage> InviteAsync(string email) =>
        PostAsync(factory.CreateMemberClient(), "/api/organization/invitations", new InviteMember(email, OrganizationRole.Editor));
}
