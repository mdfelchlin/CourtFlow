using CourtFlow.Application.Email;
using CourtFlow.Domain;

namespace CourtFlow.Application.Access;

/// <summary>The 15-minute check on waiting access requests: reminders, then expiries, then entries deleted in WorkOS.</summary>
public sealed class AccessRequestSweep(IAccessRepository repository, IWorkOs workOs, IEmailOutbox outbox, WebOptions web)
{
    public sealed record Result(int Reminded, int Expired);

    public async Task<Result> RunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var waiting = await repository.WaitingAccessRequestsAsync(cancellationToken);
        var reminded = 0;
        var expired = 0;

        foreach (var request in waiting)
        {
            if (request.ExpiryDue(now))
            {
                if (request.WaitlistEntryId.Length > 0)
                    await workOs.DeleteWaitlistEntryAsync(request.WaitlistEntryId, cancellationToken);

                request.Expire();
                expired++;
                continue;
            }

            if (request.WaitlistEntryId.Length > 0
                && !await workOs.WaitlistEntryExistsAsync(request.WaitlistEntryId, request.Email, cancellationToken))
            {
                request.Expire();
                expired++;
                continue;
            }

            if (request.ReminderDue(now))
            {
                outbox.Enqueue(EmailTemplates.RequestExpiring(request, web.Link("/")));
                request.MarkReminderSent(now);
                reminded++;
            }
        }

        if (reminded > 0 || expired > 0)
            await repository.SaveChangesAsync(cancellationToken);

        return new Result(reminded, expired);
    }
}