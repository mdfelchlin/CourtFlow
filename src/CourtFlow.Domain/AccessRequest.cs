namespace CourtFlow.Domain;

public enum AccessRequestState
{
    Waiting,
    Approved,
    Declined,
    Expired,
}

/// <summary>CourtFlow's side of a WorkOS Waitlist entry: the extra details, the operator's decision, and the 7-day expiry with its reminder.</summary>
public sealed class AccessRequest
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    public static readonly TimeSpan ReminderBefore = TimeSpan.FromHours(24);

    public const int NameMaxLength = 100;
    public const int OrganizationNameMaxLength = 100;
    public const int MessageMaxLength = 1000;

    public const string NameRequired = "Name is required.";
    public const string NameTooLong = "Name must be 100 characters or fewer.";
    public const string EmailRequired = "Email is required.";
    public const string EmailInvalid = "Enter a valid email address.";
    public const string OrganizationNameRequired = "Organization name is required.";
    public const string OrganizationNameTooLong = "Organization name must be 100 characters or fewer.";
    public const string MessageTooLong = "Message must be 1,000 characters or fewer.";
    public const string NotWaiting = "This request has already been handled.";

    public Guid Id { get; private set; }
    public string WaitlistEntryId { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string OrganizationName { get; private set; } = "";
    public string Message { get; private set; } = "";
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ReminderSentAt { get; private set; }
    public AccessRequestState State { get; private set; } = AccessRequestState.Waiting;

    private AccessRequest()
    {
    }

    public static AccessRequest Create(
        string waitlistEntryId,
        string? name,
        string? email,
        string? organizationName,
        string? message,
        DateTimeOffset now)
    {
        Validate(name, email, organizationName, message);
        return new AccessRequest
        {
            Id = Guid.NewGuid(),
            WaitlistEntryId = waitlistEntryId,
            Name = name!.Trim(),
            Email = EmailAddress.Normalize(email),
            OrganizationName = organizationName!.Trim(),
            Message = (message ?? "").Trim(),
            RequestedAt = now,
            ExpiresAt = now + Lifetime,
        };
    }

    /// <summary>The form's rules, shared by the API and the Blazor form. Throws the first problem found.</summary>
    public static void Validate(string? name, string? email, string? organizationName, string? message)
    {
        if (Problem(name, email, organizationName, message) is { } problem)
            throw new DomainRuleException(problem);
    }

    public static string? Problem(string? name, string? email, string? organizationName, string? message) =>
        NameProblem(name) ?? EmailProblem(email) ?? OrganizationNameProblem(organizationName) ?? MessageProblem(message);

    public static string? NameProblem(string? name) =>
        string.IsNullOrWhiteSpace(name) ? NameRequired
        : name.Trim().Length > NameMaxLength ? NameTooLong
        : null;

    public static string? EmailProblem(string? email) =>
        string.IsNullOrWhiteSpace(email) ? EmailRequired
        : !EmailAddress.IsValid(email) ? EmailInvalid
        : null;

    public static string? OrganizationNameProblem(string? organizationName) =>
        string.IsNullOrWhiteSpace(organizationName) ? OrganizationNameRequired
        : organizationName.Trim().Length > OrganizationNameMaxLength ? OrganizationNameTooLong
        : null;

    public static string? MessageProblem(string? message) =>
        (message ?? "").Trim().Length > MessageMaxLength ? MessageTooLong : null;

    public bool ReminderDue(DateTimeOffset now) =>
        State == AccessRequestState.Waiting && ReminderSentAt is null && now >= ExpiresAt - ReminderBefore;

    public bool ExpiryDue(DateTimeOffset now) => State == AccessRequestState.Waiting && now >= ExpiresAt;

    public void Approve() => MoveTo(AccessRequestState.Approved);

    public void Decline() => MoveTo(AccessRequestState.Declined);

    public void Expire() => MoveTo(AccessRequestState.Expired);

    public void MarkReminderSent(DateTimeOffset now)
    {
        EnsureWaiting();
        ReminderSentAt = now;
    }

    private void MoveTo(AccessRequestState state)
    {
        EnsureWaiting();
        State = state;
    }

    private void EnsureWaiting()
    {
        if (State != AccessRequestState.Waiting)
            throw new DomainRuleException(NotWaiting);
    }
}
