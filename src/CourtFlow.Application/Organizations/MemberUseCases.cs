using CourtFlow.Application.Access;
using CourtFlow.Application.Email;
using CourtFlow.Domain;

namespace CourtFlow.Application.Organizations;

/// <summary>An owner managing their own organization. The Owner policy has already checked the caller.</summary>
public sealed class MemberUseCases(
    IAccessRepository repository,
    ICurrentAccess access,
    IWorkOs workOs,
    IEmailOutbox outbox,
    TimeProvider clock,
    WebOptions web)
{
    public async Task<OrganizationOverview> GetAsync(CancellationToken cancellationToken)
    {
        var organization = await OwnOrganizationAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var invitations = await repository.InvitationsForOrganizationAsync(organization.Id, cancellationToken);

        return new OrganizationOverview(
            organization.Id,
            organization.Name,
            organization.Memberships
                .OrderBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(member => member.Email, StringComparer.Ordinal)
                .Select(member => new MemberDetails(
                    member.Id,
                    member.Name,
                    member.Email,
                    member.Role,
                    organization.IsLastOwner(member),
                    member.UserId == access.UserId))
                .ToList(),
            invitations
                .Where(invitation => invitation.State == InvitationState.Pending)
                .OrderBy(invitation => invitation.SentAt)
                .Select(invitation => new InvitationDetails(
                    invitation.Id,
                    invitation.Email,
                    invitation.Role,
                    invitation.SentAt,
                    invitation.ExpiresAt,
                    invitation.IsExpired(now)))
                .ToList());
    }

    public async Task InviteAsync(InviteMember command, CancellationToken cancellationToken)
    {
        var organization = await OwnOrganizationAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var email = await InvitationRules.EnsureCanInviteAsync(repository, organization, command.Email, now, cancellationToken);
        var role = command.Role ?? OrganizationRole.Editor;

        var sent = await workOs.SendInvitationAsync(email, cancellationToken);
        var invitation = Invitation.Create(organization.Id, email, role, access.UserId, null, sent.Id, now);
        repository.AddInvitation(invitation);
        Announce(organization, invitation, sent);
        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Always a fresh link: the old WorkOS invitation is revoked and a new one gets a fresh 7 days.</summary>
    public async Task<bool> ResendAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var organization = await OwnOrganizationAsync(cancellationToken);
        var invitation = await repository.GetInvitationAsync(invitationId, cancellationToken);
        if (invitation is null || invitation.OrganizationId != organization.Id)
            return false;
        if (invitation.State == InvitationState.Accepted)
            throw new DomainRuleException(Invitation.AlreadyAccepted);

        if (invitation.WorkOsInvitationId.Length > 0)
            await workOs.RevokeInvitationAsync(invitation.WorkOsInvitationId, cancellationToken);

        var sent = await workOs.SendInvitationAsync(invitation.Email, cancellationToken);
        invitation.Reissue(sent.Id, clock.GetUtcNow());
        Announce(organization, invitation, sent);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CancelAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var organization = await OwnOrganizationAsync(cancellationToken);
        var invitation = await repository.GetInvitationAsync(invitationId, cancellationToken);
        if (invitation is null || invitation.OrganizationId != organization.Id)
            return false;

        invitation.Cancel();
        if (invitation.WorkOsInvitationId.Length > 0)
            await workOs.RevokeInvitationAsync(invitation.WorkOsInvitationId, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ChangeRoleAsync(Guid membershipId, ChangeMemberRole command, CancellationToken cancellationToken)
    {
        var organization = await OwnOrganizationAsync(cancellationToken);
        if (organization.Memberships.All(member => member.Id != membershipId))
            return false;
        if (command.Role is not { } role)
            throw new DomainRuleException("Role is required.");

        organization.ChangeRole(membershipId, role);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveAsync(Guid membershipId, CancellationToken cancellationToken)
    {
        var organization = await OwnOrganizationAsync(cancellationToken);
        if (organization.Memberships.All(member => member.Id != membershipId))
            return false;

        organization.RemoveMember(membershipId);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private void Announce(Organization organization, Invitation invitation, WorkOsInvitation sent)
    {
        var inviter = string.IsNullOrWhiteSpace(access.Name) ? access.Email ?? "A CourtFlow owner" : access.Name;
        outbox.Enqueue(EmailTemplates.OwnerInvitation(
            invitation.Email,
            inviter,
            organization.Name,
            invitation.Role,
            sent.AcceptUrl ?? web.Link(SignInUseCases.Tournaments),
            invitation.ExpiresAt));
    }

    private async Task<Organization> OwnOrganizationAsync(CancellationToken cancellationToken)
    {
        if (!access.IsOwner())
            throw new InvalidOperationException("Only an owner of an active organization manages its members.");

        return await repository.GetOrganizationAsync(access.Membership!.OrganizationId, cancellationToken)
            ?? throw new InvalidOperationException("The caller's organization was not found.");
    }
}