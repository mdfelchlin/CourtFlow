namespace CourtFlow.Application.Abstractions;

public interface ITenantContext
{
    Guid? OrganizationId { get; }
    Guid? UserId { get; }
    bool IsAuthenticated { get; }

    Guid RequireOrganizationId();
    Guid RequireUserId();
}
