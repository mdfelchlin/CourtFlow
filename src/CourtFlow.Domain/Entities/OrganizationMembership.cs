using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class OrganizationMembership : Entity
{
    public Guid OrganizationId { get; private set; }

    /// <summary>Clerk <c>sub</c> or other external IdP user id.</summary>
    public string ExternalUserId { get; private set; } = "";

    public string Role { get; private set; } = "Admin";

    private OrganizationMembership() { }

    public OrganizationMembership(Guid organizationId, string externalUserId, string role = "Admin")
    {
        OrganizationId = organizationId;
        ExternalUserId = externalUserId;
        Role = role;
    }
}
