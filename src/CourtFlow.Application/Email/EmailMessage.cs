namespace CourtFlow.Application.Email;

public sealed record EmailMessage(IReadOnlyList<string> To, string Subject, string Text, string Html);

/// <summary>Adds an email to the current unit of work. It is saved with the action that caused it and sent later.</summary>
public interface IEmailOutbox
{
    void Enqueue(EmailMessage message);
}

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}