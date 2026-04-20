using System.Security.Claims;
using CourtFlow.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace CourtFlow.Infrastructure.MultiTenancy;

public class HttpTenantContext : ITenantContext
{
    public const string OrgIdClaim = "org_id";

    private static readonly AsyncLocal<(Guid OrgId, Guid? UserId)?> Override = new();

    private readonly IHttpContextAccessor _accessor;

    public HttpTenantContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    /// <summary>
    /// Establishes an ambient tenant override for the current async flow. Use for seeding,
    /// background jobs, or tests. Dispose to restore the previous value.
    /// </summary>
    public static IDisposable BeginScope(Guid organizationId, Guid? userId = null)
    {
        var previous = Override.Value;
        Override.Value = (organizationId, userId);
        return new Releaser(previous);
    }

    public Guid? OrganizationId
    {
        get
        {
            if (Override.Value is { } overriden) return overriden.OrgId;
            var user = _accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return null;
            var orgClaim = user.FindFirstValue(OrgIdClaim);
            return Guid.TryParse(orgClaim, out var id) ? id : null;
        }
    }

    public Guid? UserId
    {
        get
        {
            if (Override.Value is { } overriden) return overriden.UserId;
            var user = _accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return null;
            var sub = user.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => OrganizationId.HasValue;

    public Guid RequireOrganizationId() =>
        OrganizationId ?? throw new InvalidOperationException("No organization context available");

    public Guid RequireUserId() =>
        UserId ?? throw new InvalidOperationException("No user context available");

    private sealed class Releaser : IDisposable
    {
        private readonly (Guid OrgId, Guid? UserId)? _previous;
        public Releaser((Guid OrgId, Guid? UserId)? previous) => _previous = previous;
        public void Dispose() => Override.Value = _previous;
    }
}

public sealed class NullTenantContext : ITenantContext
{
    public Guid? OrganizationId => null;
    public Guid? UserId => null;
    public bool IsAuthenticated => false;
    public Guid RequireOrganizationId() => throw new InvalidOperationException("No organization context available");
    public Guid RequireUserId() => throw new InvalidOperationException("No user context available");
}
