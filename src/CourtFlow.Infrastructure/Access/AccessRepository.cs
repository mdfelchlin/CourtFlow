using CourtFlow.Application.Access;
using CourtFlow.Domain;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace CourtFlow.Infrastructure.Access;

public sealed class AccessRepository(CourtFlowDbContext db) : IAccessRepository
{
    public Task<Organization?> GetOrganizationAsync(Guid id, CancellationToken cancellationToken) =>
        WithMembers().FirstOrDefaultAsync(organization => organization.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Organization>> ListOrganizationsAsync(CancellationToken cancellationToken) =>
        await WithMembers().ToListAsync(cancellationToken);

    public void AddOrganization(Organization organization) => db.Organizations.Add(organization);

    public async Task<IReadOnlyList<Organization>> OrganizationsOfUserAsync(string userId, CancellationToken cancellationToken) =>
        await WithMembers()
            .Where(organization => organization.Memberships.Any(member => member.UserId == userId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Organization>> OrganizationsWithMemberEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = EmailAddress.Normalize(email);
        return await WithMembers()
            .Where(organization => organization.Memberships.Any(member => member.Email == normalized))
            .ToListAsync(cancellationToken);
    }

    public Task<Invitation?> GetInvitationAsync(Guid id, CancellationToken cancellationToken) =>
        db.Invitations.FirstOrDefaultAsync(invitation => invitation.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Invitation>> InvitationsForEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = EmailAddress.Normalize(email);
        return await db.Invitations.Where(invitation => invitation.Email == normalized).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Invitation>> InvitationsForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await db.Invitations.Where(invitation => invitation.OrganizationId == organizationId).ToListAsync(cancellationToken);

    public void AddInvitation(Invitation invitation) => db.Invitations.Add(invitation);

    public Task<AccessRequest?> GetAccessRequestAsync(Guid id, CancellationToken cancellationToken) =>
        db.AccessRequests.FirstOrDefaultAsync(request => request.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AccessRequest>> AccessRequestsForEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = EmailAddress.Normalize(email);
        return await db.AccessRequests.Where(request => request.Email == normalized).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccessRequest>> WaitingAccessRequestsAsync(CancellationToken cancellationToken) =>
        await db.AccessRequests
            .Where(request => request.State == AccessRequestState.Waiting)
            .OrderBy(request => request.RequestedAt)
            .ToListAsync(cancellationToken);

    public void AddAccessRequest(AccessRequest request) => db.AccessRequests.Add(request);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DomainRuleException(Organization.AlreadyAMember);
        }
    }

    private IQueryable<Organization> WithMembers() => db.Organizations.Include(organization => organization.Memberships);
}