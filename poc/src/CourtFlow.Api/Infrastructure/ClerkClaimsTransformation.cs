using System.Security.Claims;
using CourtFlow.Domain.Entities;
using CourtFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Api.Infrastructure;

/// <summary>
/// Bridges Clerk-issued JWT claims to CourtFlow's internal identity model:
///
/// 1. Normalizes the standard <c>sub</c> / <c>email</c> claims into the
///    .NET equivalents (<see cref="ClaimTypes.NameIdentifier"/>, etc.).
/// 2. Resolves the user's primary <see cref="Organization"/> from
///    <see cref="OrganizationMembership"/> — auto-provisioning a personal
///    workspace on first sight. Personal workspaces have a <c>NULL</c>
///    <see cref="Organization.ClerkOrgId"/> and consume no Clerk org slots.
/// 3. Overwrites any <c>org_id</c>/<c>org_role</c> claims emitted by Clerk
///    (which contain unresolved <c>{{organization.id}}</c> placeholders when
///    the session is in personal-account mode) with the **internal**
///    <see cref="Organization.Id"/> Guid and the membership's role. This
///    keeps every downstream consumer (<c>RequireOrg</c> policy,
///    <c>HttpTenantContext</c>) happy without forcing the user to be inside
///    a Clerk Organization.
/// </summary>
public sealed class ClerkClaimsTransformation : IClaimsTransformation
{
    private const string OrgIdClaim = "org_id";
    private const string OrgRoleClaim = "org_role";

    private readonly AppDbContext _db;
    private readonly ILogger<ClerkClaimsTransformation> _log;

    public ClerkClaimsTransformation(AppDbContext db, ILogger<ClerkClaimsTransformation> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return principal;

        EnsureClaim(identity, ClaimTypes.NameIdentifier, "sub");
        EnsureClaim(identity, ClaimTypes.Email, "email");
        EnsureClaim(identity, ClaimTypes.Email, "email_address");
        EnsureClaim(identity, ClaimTypes.Name, ClaimTypes.Email);

        var sub = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? identity.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(sub))
        {
            _log.LogDebug("Authenticated principal has no sub claim; skipping tenant resolution.");
            return principal;
        }

        var membership = await ResolveOrProvisionAsync(sub, identity.FindFirst(ClaimTypes.Email)?.Value);

        ReplaceClaim(identity, OrgIdClaim, membership.OrganizationId.ToString());
        ReplaceClaim(identity, OrgRoleClaim, membership.Role);
        ReplaceClaim(identity, ClaimTypes.Role,
            string.Equals(membership.Role, "admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : membership.Role);

        return principal;
    }

    private async Task<OrganizationMembership> ResolveOrProvisionAsync(string sub, string? email)
    {
        var existing = await _db.OrganizationMemberships
            .AsNoTracking()
            .Where(m => m.ExternalUserId == sub)
            .OrderBy(m => m.CreatedAtUtc)
            .FirstOrDefaultAsync();
        if (existing is not null)
            return existing;

        try
        {
            var (name, slug) = BuildPersonalIdentifiers(sub, email);
            var org = Organization.CreatePersonal(sub, name, slug);
            var membership = new OrganizationMembership(org.Id, sub, role: "Admin");
            _db.Organizations.Add(org);
            _db.OrganizationMemberships.Add(membership);
            await _db.SaveChangesAsync();
            _log.LogInformation("Auto-provisioned personal workspace {OrgId} for Clerk user {Sub}", org.Id, sub);
            return membership;
        }
        catch (DbUpdateException ex)
        {
            // Lost a race to another request also handling this user's first sight —
            // re-read the row the winner inserted.
            _log.LogDebug(ex,
                "Personal workspace insert failed for {Sub} (likely concurrent provisioning) — re-reading.", sub);
            _db.ChangeTracker.Clear();
            var winner = await _db.OrganizationMemberships
                .AsNoTracking()
                .Where(m => m.ExternalUserId == sub)
                .OrderBy(m => m.CreatedAtUtc)
                .FirstOrDefaultAsync();
            if (winner is null) throw;
            return winner;
        }
    }

    private static (string Name, string Slug) BuildPersonalIdentifiers(string sub, string? email)
    {
        var localPart = !string.IsNullOrWhiteSpace(email) && email.Contains('@')
            ? email[..email.IndexOf('@')]
            : null;
        var name = !string.IsNullOrWhiteSpace(localPart)
            ? $"{localPart}'s workspace"
            : "Personal workspace";

        // Slug is the Clerk sub lowercased — short, stable, unique, and identifiable in the
        // database. Strips the conventional `user_` prefix Clerk emits to keep slugs readable.
        var subTail = sub.StartsWith("user_", StringComparison.OrdinalIgnoreCase) ? sub[5..] : sub;
        var slug = "user-" + new string(subTail.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (slug.Length > 100) slug = slug[..100];
        return (name, slug);
    }

    private static void EnsureClaim(ClaimsIdentity identity, string targetClaimType, string sourceClaimType)
    {
        if (identity.HasClaim(c => c.Type == targetClaimType)) return;
        var source = identity.FindFirst(sourceClaimType);
        if (source is not null && !string.IsNullOrWhiteSpace(source.Value))
            identity.AddClaim(new Claim(targetClaimType, source.Value));
    }

    private static void ReplaceClaim(ClaimsIdentity identity, string claimType, string value)
    {
        foreach (var existing in identity.FindAll(claimType).ToList())
            identity.RemoveClaim(existing);
        identity.AddClaim(new Claim(claimType, value));
    }
}
