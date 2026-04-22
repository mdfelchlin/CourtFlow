using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace CourtFlow.Api.Infrastructure;

public sealed class ClerkClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        EnsureClaim(identity, ClaimTypes.NameIdentifier, "sub");
        EnsureClaim(identity, ClaimTypes.Name, "email");
        EnsureClaim(identity, ClaimTypes.Name, "email_address");
        EnsureClaim(identity, "org_id", "org_id");
        EnsureClaim(identity, "org_id", "orgId");
        EnsureRoleClaim(identity);

        return Task.FromResult(principal);
    }

    private static void EnsureClaim(ClaimsIdentity identity, string targetClaimType, string sourceClaimType)
    {
        if (identity.HasClaim(c => c.Type == targetClaimType))
        {
            return;
        }

        var source = identity.FindFirst(sourceClaimType);
        if (source is not null && !string.IsNullOrWhiteSpace(source.Value))
        {
            identity.AddClaim(new Claim(targetClaimType, source.Value));
        }
    }

    private static void EnsureRoleClaim(ClaimsIdentity identity)
    {
        if (identity.HasClaim(c => c.Type == ClaimTypes.Role))
        {
            return;
        }

        var clerkRole = identity.FindFirst("org_role")?.Value
            ?? identity.FindFirst("orgRole")?.Value
            ?? identity.FindFirst("role")?.Value;
        if (string.IsNullOrWhiteSpace(clerkRole))
        {
            return;
        }

        var normalized = clerkRole.Equals("admin", StringComparison.OrdinalIgnoreCase)
            ? "Admin"
            : clerkRole;
        identity.AddClaim(new Claim(ClaimTypes.Role, normalized));
    }
}
