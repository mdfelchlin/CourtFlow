using System.Net.Mail;
using System.Net.Mime;
using CourtFlow.Application.Email;

namespace CourtFlow.Infrastructure.Email;

/// <summary>Local runs send to the Mailpit inbox Aspire starts.</summary>
public sealed class SmtpEmailSender(EmailOptions options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var address = new Uri(options.Smtp);
        using var client = new SmtpClient(address.Host, address.Port);
        using var mail = new MailMessage
        {
            From = new MailAddress(options.Domain.Length > 0 ? $"no-reply@{options.Domain}" : "no-reply@courtflow.local", "CourtFlow"),
            Subject = message.Subject,
            Body = message.Text,
        };
        foreach (var to in message.To)
            mail.To.Add(to);
        if (!string.IsNullOrWhiteSpace(options.ReplyTo))
            mail.ReplyToList.Add(options.ReplyTo);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.Html, null, MediaTypeNames.Text.Html));

        await client.SendMailAsync(mail, cancellationToken);
    }
}
