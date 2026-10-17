using CourtFlow.Domain;

namespace CourtFlow.Application.Access;

/// <summary>Organizations with their members, invitations, and access requests. None of these are filtered by organization; the use cases check access themselves.</summary>
public interface IAccessRepository
{
    Task<Organization?> GetOrganizationAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Organization>> ListOrganizationsAsync(CancellationToken cancellationToken);
    void AddOrganization(Organization organization);

    Task<IReadOnlyList<Organization>> OrganizationsOfUserAsync(string userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Organization>> OrganizationsWithMemberEmailAsync(string email, CancellationToken cancellationToken);

    Task<Invitation?> GetInvitationAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Invitation>> InvitationsForEmailAsync(string email, CancellationToken cancellationToken);
    Task<IReadOnlyList<Invitation>> InvitationsForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
    void AddInvitation(Invitation invitation);

    Task<AccessRequest?> GetAccessRequestAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccessRequest>> AccessRequestsForEmailAsync(string email, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccessRequest>> WaitingAccessRequestsAsync(CancellationToken cancellationToken);
    void AddAccessRequest(AccessRequest request);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
