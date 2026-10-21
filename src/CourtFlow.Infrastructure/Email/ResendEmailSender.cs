using System.Net.Http.Headers;
using System.Net.Http.Json;

using CourtFlow.Application.Email;

namespace CourtFlow.Infrastructure.Email;

/// <summary>Deployed environments send through Resend's REST API.</summary>
public sealed class ResendEmailSender(HttpClient http, ResendOptions resend, EmailOptions email) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(new
            {
                from = email.From,
                to = message.To,
                subject = message.Subject,
                html = message.Html,
                text = message.Text,
                reply_to = string.IsNullOrWhiteSpace(email.ReplyTo) ? null : email.ReplyTo,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resend.ApiKey);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Resend answered {(int)response.StatusCode}: {body}");
        }
    }
}