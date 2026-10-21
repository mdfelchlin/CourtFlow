using CourtFlow.Application.Access;
using CourtFlow.Application.Email;
using CourtFlow.Domain;

namespace CourtFlow.Application.Organizations;

/// <summary>An operator's view of every organization. The Operator policy has already checked the caller.</summary>
public sealed class OperatorOrganizationUseCases(
    IAccessRepository repository,
    ICurrentAccess access,
    IWorkOs workOs,
    IEmailOutbox outbox,
    TimeProvider clock,
    WebOptions web)
{
    public async Task<IReadOnlyList<OperatorOrganizationDetails>> ListAsync(CancellationToken cancellationToken) =>
        (await repository.ListOrganizationsAsync(cancellationToken))
            .OrderBy(organization => organization.Name, StringComparer.OrdinalIgnoreCase)
            .Select(organization => new OperatorOrganizationDetails(
                organization.Id,
                organization.Name,
                organization.Status,
                organization.Memberships.Count,
                organization.Memberships
                    .Where(member => member.Role == OrganizationRole.Owner)
                    .Select(member => string.IsNullOrWhiteSpace(member.Name) ? member.Email : member.Name)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .ToList();

    /// <summary>How the Test Organization gets its first real owner. Refused for the same reasons as an owner's invitation.</summary>
    public async Task<bool> InviteOwnerAsync(Guid organizationId, InviteOwner command, CancellationToken cancellationToken)
    {
        var organization = await repository.GetOrganizationAsync(organizationId, cancellationToken);
        if (organization is null)
            return false;

        var now = clock.GetUtcNow();
        var email = await InvitationRules.EnsureCanInviteAsync(repository, organization, command.Email, now, cancellationToken);
        var sent = await workOs.SendInvitationAsync(email, cancellationToken);
        var invitation = Invitation.Create(organization.Id, email, OrganizationRole.Owner, null, access.Email, sent.Id, now);
        repository.AddInvitation(invitation);
        outbox.Enqueue(EmailTemplates.OperatorInvitation(
            email,
            organization.Name,
            sent.AcceptUrl ?? web.Link(SignInUseCases.Tournaments),
            invitation.ExpiresAt));
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Members lose access on their next call. Pending invitations are cancelled and stay cancelled after a reactivation.</summary>
    public async Task<bool> DeactivateAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var organization = await repository.GetOrganizationAsync(organizationId, cancellationToken);
        if (organization is null)
            return false;

        organization.Deactivate();
        foreach (var invitation in await repository.InvitationsForOrganizationAsync(organization.Id, cancellationToken))
        {
            if (invitation.State != InvitationState.Pending)
                continue;

            invitation.Cancel();
            if (invitation.WorkOsInvitationId.Length > 0)
                await workOs.RevokeInvitationAsync(invitation.WorkOsInvitationId, cancellationToken);
        }

        foreach (var member in organization.Memberships)
            outbox.Enqueue(EmailTemplates.Deactivated(member.Email, member.Name, organization.Name));

        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReactivateAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var organization = await repository.GetOrganizationAsync(organizationId, cancellationToken);
        if (organization is null)
            return false;

        organization.Reactivate();
        foreach (var member in organization.Memberships)
            outbox.Enqueue(EmailTemplates.Reactivated(member.Email, member.Name, organization.Name, web.Link(SignInUseCases.Tournaments)));

        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }
}