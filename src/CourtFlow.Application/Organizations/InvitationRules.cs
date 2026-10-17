using CourtFlow.Application.Access;
using CourtFlow.Domain;

namespace CourtFlow.Application.Organizations;

/// <summary>The four reasons an invitation is refused, checked in the spec's order. Owners and operators share them.</summary>
public static class InvitationRules
{
    public const string AlreadyMember = "This person is already a member.";
    public const string BelongsElsewhere = "This person already belongs to another organization.";
    public const string AlreadyInvitedHere = "There's already an invitation waiting for this email. Use Resend instead.";
    public const string InvitedElsewhere = "This person already has an invitation to another organization.";
    public const string InvalidEmail = "Enter a valid email address.";
    public const string Deactivated = "This organization is deactivated.";

    public static async Task<string> EnsureCanInviteAsync(
        IAccessRepository repository,
        Organization organization,
        string? email,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!EmailAddress.IsValid(email))
            throw new DomainRuleException(InvalidEmail);
        if (!organization.IsActive)
            throw new DomainRuleException(Deactivated);

        var normalized = EmailAddress.Normalize(email);
        if (organization.MemberWithEmail(normalized) is not null)
            throw new DomainRuleException(AlreadyMember);

        var memberships = await repository.OrganizationsWithMemberEmailAsync(normalized, cancellationToken);
        if (memberships.Any(other => other.Id != organization.Id))
            throw new DomainRuleException(BelongsElsewhere);

        var open = (await repository.InvitationsForEmailAsync(normalized, cancellationToken)).Where(item => item.IsOpen(now)).ToList();
        if (open.Any(item => item.OrganizationId == organization.Id))
            throw new DomainRuleException(AlreadyInvitedHere);
        if (open.Count > 0)
            throw new DomainRuleException(InvitedElsewhere);

        return normalized;
    }
}
