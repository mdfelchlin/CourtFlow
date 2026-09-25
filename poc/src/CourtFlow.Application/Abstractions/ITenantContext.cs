namespace CourtFlow.Application.Abstractions;

public interface ITenantContext
{
    Guid? OrganizationId { get; }
    string? UserId { get; }
    bool IsAuthenticated { get; }

    Guid RequireOrganizationId();
    string RequireUserId();
}
