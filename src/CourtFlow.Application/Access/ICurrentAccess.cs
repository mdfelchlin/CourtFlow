using CourtFlow.Domain;

namespace CourtFlow.Application.Access;

/// <summary>The caller's one membership, active or not. Only one is ever reported in this slice.</summary>
public sealed record MembershipAccess(
    Guid MembershipId,
    Guid OrganizationId,
    string OrganizationName,
    OrganizationStatus OrganizationStatus,
    OrganizationRole Role)
{
    public bool IsActive => OrganizationStatus == OrganizationStatus.Active;
}

/// <summary>Who is calling, read once per request from the session plus one database read.</summary>
public interface ICurrentAccess
{
    bool IsSignedIn { get; }
    string? UserId { get; }
    string? Email { get; }
    string? Name { get; }
    bool IsOperator { get; }
    MembershipAccess? Membership { get; }
}

public static class CurrentAccessExtensions
{
    public static bool HasActiveMembership(this ICurrentAccess access) => access.Membership is { IsActive: true };

    /// <summary>The role, only while the membership is active.</summary>
    public static OrganizationRole? ActiveRole(this ICurrentAccess access) =>
        access.HasActiveMembership() ? access.Membership!.Role : null;

    public static bool CanEdit(this ICurrentAccess access) =>
        access.ActiveRole() is { } role && OrganizationRoles.CanEdit(role);

    public static bool IsOwner(this ICurrentAccess access) => access.ActiveRole() == OrganizationRole.Owner;
}