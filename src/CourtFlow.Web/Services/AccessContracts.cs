using CourtFlow.Domain;

namespace CourtFlow.Web.Services;

public sealed record MeResponse(bool SignedIn, string? Name, string? Email, bool IsOperator, MeOrganization? Organization);

public sealed record MeOrganization(Guid Id, string Name, OrganizationStatus Status, OrganizationRole Role);

public sealed record AccessRequestForm(string? Name, string? Email, string? OrganizationName, string? Message, string? CaptchaToken);

/// <summary>Matches the API's enum order; the API sends it as a number.</summary>
public enum AccessRequestOutcome
{
    Sent,
    CaptchaFailed,
    AlreadyHasAccess,
    AlreadyWaiting,
    Declined,
    CouldNotSend,
}

public sealed record AccessRequestResultResponse(AccessRequestOutcome Outcome, string Message);

public sealed record MemberResponse(Guid Id, string Name, string Email, OrganizationRole Role, bool IsLastOwner, bool IsYou);

public sealed record InvitationResponse(Guid Id, string Email, OrganizationRole Role, DateTimeOffset SentAt, DateTimeOffset ExpiresAt, bool IsExpired);

public sealed record OrganizationOverviewResponse(
    Guid Id,
    string Name,
    IReadOnlyList<MemberResponse> Members,
    IReadOnlyList<InvitationResponse> Invitations);

public sealed record AccessRequestResponse(
    Guid Id,
    string Name,
    string Email,
    string OrganizationName,
    string Message,
    DateTimeOffset RequestedAt,
    DateTimeOffset ExpiresAt);

/// <summary>Either <see cref="NewOrganizationName"/>, or <see cref="OrganizationId"/> with <see cref="Role"/>.</summary>
public sealed record ApproveAccessRequestRequest(string? NewOrganizationName, Guid? OrganizationId, OrganizationRole? Role);

public sealed record OperatorOrganizationResponse(
    Guid Id,
    string Name,
    OrganizationStatus Status,
    int MemberCount,
    IReadOnlyList<string> OwnerNames);