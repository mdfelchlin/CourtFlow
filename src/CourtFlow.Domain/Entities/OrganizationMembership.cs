using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class OrganizationMembership : Entity
{
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public string Role { get; private set; } = "Admin";

    private OrganizationMembership() { }

    public OrganizationMembership(Guid organizationId, Guid userId, string role = "Admin")
    {
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
    }
}
