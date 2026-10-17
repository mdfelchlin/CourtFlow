using CourtFlow.Application.Email;

namespace CourtFlow.Infrastructure.Email;

public sealed class EmailOutbox(CourtFlowDbContext db, TimeProvider clock) : IEmailOutbox
{
    public void Enqueue(EmailMessage message) => db.OutboxEmails.Add(new OutboxEmail(message, clock.GetUtcNow()));
}
