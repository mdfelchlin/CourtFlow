using CourtFlow.Application.Abstractions;

namespace CourtFlow.Infrastructure.MultiTenancy;

/// <summary>
/// Allows overriding the tenant context within a scope (e.g. seed, background jobs).
/// </summary>
public sealed class ScopedTenantContext : ITenantContext
{
    public Guid? OrganizationId { get; set; }
    public string? UserId { get; set; }
    public bool IsAuthenticated => OrganizationId.HasValue;

    public Guid RequireOrganizationId() =>
        OrganizationId ?? throw new InvalidOperationException("No organization context available");

    public string RequireUserId() =>
        UserId ?? throw new InvalidOperationException("No user context available");
}
