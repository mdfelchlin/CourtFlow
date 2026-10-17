namespace CourtFlow.Domain;

public enum InvitationState
{
    Pending,
    Accepted,
    Cancelled,
}

/// <summary>
/// CourtFlow's record of who is invited where, and as what. WorkOS holds only the matching sign-up invitation.
/// Expiry is computed from <see cref="ExpiresAt"/>, so nothing has to mark an invitation expired.
/// </summary>
public sealed class Invitation
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public const string NotPending = "This invitation is no longer pending.";
    public const string Expired = "This invitation has expired.";
    public const string AlreadyAccepted = "This invitation was already accepted.";

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Email { get; private set; } = "";
    public OrganizationRole Role { get; private set; }
    public string? SentByUserId { get; private set; }
    public string? SentByOperatorEmail { get; private set; }
    public string WorkOsInvitationId { get; private set; } = "";
    public DateTimeOffset SentAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public InvitationState State { get; private set; } = InvitationState.Pending;

    private Invitation()
    {
    }

    public static Invitation Create(
        Guid organizationId,
        string email,
        OrganizationRole role,
        string? sentByUserId,
        string? sentByOperatorEmail,
        string workOsInvitationId,
        DateTimeOffset now)
    {
        if (!EmailAddress.IsValid(email))
            throw new DomainRuleException("Enter a valid email address.");

        return new Invitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Email = EmailAddress.Normalize(email),
            Role = role,
            SentByUserId = sentByUserId,
            SentByOperatorEmail = sentByOperatorEmail is null ? null : EmailAddress.Normalize(sentByOperatorEmail),
            WorkOsInvitationId = workOsInvitationId,
            SentAt = now,
            ExpiresAt = now + Lifetime,
        };
    }

    public bool IsExpired(DateTimeOffset now) => State == InvitationState.Pending && now >= ExpiresAt;

    /// <summary>Pending and not yet expired: the only kind that can be accepted or that blocks another invitation.</summary>
    public bool IsOpen(DateTimeOffset now) => State == InvitationState.Pending && now < ExpiresAt;

    public void Accept(DateTimeOffset now)
    {
        if (State != InvitationState.Pending)
            throw new DomainRuleException(NotPending);
        if (IsExpired(now))
            throw new DomainRuleException(Expired);

        State = InvitationState.Accepted;
    }

    public void Cancel()
    {
        if (State != InvitationState.Pending)
            throw new DomainRuleException(NotPending);

        State = InvitationState.Cancelled;
    }

    /// <summary>A fresh link with a fresh 7 days. Brings back a cancelled or expired invitation, but never an accepted one.</summary>
    public void Reissue(string workOsInvitationId, DateTimeOffset now)
    {
        if (State == InvitationState.Accepted)
            throw new DomainRuleException(AlreadyAccepted);

        WorkOsInvitationId = workOsInvitationId;
        SentAt = now;
        ExpiresAt = now + Lifetime;
        State = InvitationState.Pending;
    }
}
