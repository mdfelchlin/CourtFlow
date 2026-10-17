using CourtFlow.Application.Email;
using Microsoft.Extensions.Logging;

namespace CourtFlow.Infrastructure.Email;

/// <summary>When neither Mailpit nor Resend is configured, such as running the API on its own, emails are written to the log.</summary>
public sealed class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Email to {To}: {Subject}\n{Text}", string.Join(", ", message.To), message.Subject, message.Text);
        return Task.CompletedTask;
    }
}
