using CourtFlow.Application.Email;

namespace CourtFlow.Api.Tests;

public sealed class CapturingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];
    public bool Fails { get; set; }
    public int Attempts { get; private set; }

    public void Reset()
    {
        Sent.Clear();
        Fails = false;
        Attempts = 0;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Attempts++;
        if (Fails)
            throw new InvalidOperationException("The email provider is down in this test.");
        Sent.Add(message);
        return Task.CompletedTask;
    }
}
