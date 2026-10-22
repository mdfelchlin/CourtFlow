using CourtFlow.Application.Email;

using Microsoft.Extensions.Logging;

namespace CourtFlow.Infrastructure.Email;

/// <summary>When neither Mailpit nor Resend is configured, such as running the API on its own, emails are written to the log.</summary>
public sealed partial class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            var recipients = string.Join(", ", message.To);
            LogEmail(recipients, message.Subject, message.Text);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email to {To}: {Subject}\n{Text}")]
    private partial void LogEmail(string to, string subject, string text);
}