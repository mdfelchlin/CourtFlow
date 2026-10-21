using CourtFlow.Domain;

namespace CourtFlow.Application.Access;

public sealed record MeResponse(bool SignedIn, string? Name, string? Email, bool IsOperator, MeOrganization? Organization);

public sealed record MeOrganization(Guid Id, string Name, OrganizationStatus Status, OrganizationRole Role);

public sealed record OrganizationOverview(
    Guid Id,
    string Name,
    IReadOnlyList<MemberDetails> Members,
    IReadOnlyList<InvitationDetails> Invitations);

public sealed record MemberDetails(Guid Id, string Name, string Email, OrganizationRole Role, bool IsLastOwner, bool IsYou);

public sealed record InvitationDetails(Guid Id, string Email, OrganizationRole Role, DateTimeOffset SentAt, DateTimeOffset ExpiresAt, bool IsExpired);

public sealed record InviteMember(string? Email, OrganizationRole? Role);

public sealed record ChangeMemberRole(OrganizationRole? Role);

public sealed record SubmitAccessRequest(string? Name, string? Email, string? OrganizationName, string? Message, string? CaptchaToken);

public enum AccessRequestOutcome
{
    Sent,
    CaptchaFailed,
    AlreadyHasAccess,
    AlreadyWaiting,
    Declined,
    CouldNotSend,
}

public sealed record AccessRequestResult(AccessRequestOutcome Outcome, string Message);

public sealed record AccessRequestDetails(
    Guid Id,
    string Name,
    string Email,
    string OrganizationName,
    string Message,
    DateTimeOffset RequestedAt,
    DateTimeOffset ExpiresAt);

/// <summary>Either <see cref="NewOrganizationName"/>, or <see cref="OrganizationId"/> with <see cref="Role"/>.</summary>
public sealed record ApproveAccessRequest(string? NewOrganizationName, Guid? OrganizationId, OrganizationRole? Role);

public sealed record OperatorOrganizationDetails(
    Guid Id,
    string Name,
    OrganizationStatus Status,
    int MemberCount,
    IReadOnlyList<string> OwnerNames);

public sealed record InviteOwner(string? Email);