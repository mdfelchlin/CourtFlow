using CourtFlow.Application.Email;

namespace CourtFlow.Infrastructure.Email;

/// <summary>An email waiting to be sent, saved in the same change as the action that caused it.</summary>
public sealed class OutboxEmail
{
    public Guid Id { get; private set; }
    public List<string> To { get; private set; } = [];
    public string Subject { get; private set; } = "";
    public string Text { get; private set; } = "";
    public string Html { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public int Attempts { get; private set; }

    /// <summary>When the next attempt is due, or null once it is sent or has given up.</summary>
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string? LastError { get; private set; }

    private OutboxEmail()
    {
    }

    public OutboxEmail(EmailMessage message, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        To = message.To.ToList();
        Subject = message.Subject;
        Text = message.Text;
        Html = message.Html;
        CreatedAt = now;
        NextAttemptAt = now;
    }

    public EmailMessage ToMessage() => new(To, Subject, Text, Html);

    public void MarkSent(DateTimeOffset now)
    {
        Attempts++;
        SentAt = now;
        NextAttemptAt = null;
        LastError = null;
    }

    /// <summary>Schedules the next retry from <paramref name="retryDelays"/>, or gives up when they run out.</summary>
    public void MarkFailed(DateTimeOffset now, string error, IReadOnlyList<TimeSpan> retryDelays)
    {
        Attempts++;
        LastError = error;
        NextAttemptAt = Attempts <= retryDelays.Count ? now + retryDelays[Attempts - 1] : null;
    }
}