using CourtFlow.Application.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CourtFlow.Infrastructure.Email;

/// <summary>Sends due outbox rows. A failure is retried after 1, 5, 10, 20, and 30 minutes, then given up and left logged.</summary>
public sealed class EmailOutboxSender(CourtFlowDbContext db, IEmailSender sender, ILogger<EmailOutboxSender> logger)
{
    public static readonly IReadOnlyList<TimeSpan> RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(20),
        TimeSpan.FromMinutes(30),
    ];

    private const int BatchSize = 50;

    public async Task<int> SendDueAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var due = await db.OutboxEmails
            .Where(email => email.SentAt == null && email.NextAttemptAt != null && email.NextAttemptAt <= now)
            .OrderBy(email => email.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var email in due)
        {
            try
            {
                await sender.SendAsync(email.ToMessage(), cancellationToken);
                email.MarkSent(now);
                sent++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                email.MarkFailed(now, exception.Message, RetryDelays);
                if (email.NextAttemptAt is null)
                    logger.LogError(exception, "Gave up sending email {Id} ({Subject}) after {Attempts} attempts.", email.Id, email.Subject, email.Attempts);
                else
                    logger.LogWarning(exception, "Sending email {Id} ({Subject}) failed. It will be retried at {NextAttemptAt}.", email.Id, email.Subject, email.NextAttemptAt);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return sent;
    }
}
