using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class Organization : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;

    /// <summary>
    /// Clerk Organization id when this workspace is backed by a Clerk org (team workspace).
    /// NULL means a personal workspace that auto-provisions for a single user — no Clerk
    /// org slot is consumed. Set during an explicit "upgrade to team workspace" flow.
    /// </summary>
    public string? ClerkOrgId { get; private set; }

    /// <summary>
    /// Clerk <c>sub</c> of the user who owns this personal workspace. NULL on team
    /// workspaces (membership is the source of truth there). Unique across personal
    /// workspaces so a single Clerk user gets at most one personal Organization.
    /// </summary>
    public string? OwnerExternalUserId { get; private set; }

    private readonly List<OrganizationMembership> _memberships = new();
    public IReadOnlyCollection<OrganizationMembership> Memberships => _memberships.AsReadOnly();

    private readonly List<Tournament> _tournaments = new();
    public IReadOnlyCollection<Tournament> Tournaments => _tournaments.AsReadOnly();

    private Organization() { }

    public Organization(string name, string slug)
    {
        Rename(name);
        SetSlug(slug);
    }

    /// <summary>
    /// Creates a personal workspace Organization owned by a single Clerk user.
    /// Personal workspaces have no <see cref="ClerkOrgId"/> and consume no Clerk
    /// org slots. Use <see cref="UpgradeToTeamWorkspace"/> later to convert.
    /// </summary>
    public static Organization CreatePersonal(string ownerExternalUserId, string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(ownerExternalUserId))
            throw new ArgumentException("Owner external user id required", nameof(ownerExternalUserId));

        var org = new Organization(name, slug)
        {
            OwnerExternalUserId = ownerExternalUserId.Trim()
        };
        return org;
    }

    /// <summary>
    /// Upgrades a personal workspace to a team workspace backed by a real Clerk
    /// Organization. Idempotent if already linked to the same Clerk org.
    /// </summary>
    public void UpgradeToTeamWorkspace(string clerkOrgId)
    {
        if (string.IsNullOrWhiteSpace(clerkOrgId))
            throw new ArgumentException("Clerk org id required", nameof(clerkOrgId));
        if (ClerkOrgId is not null && !string.Equals(ClerkOrgId, clerkOrgId, StringComparison.Ordinal))
            throw new InvalidOperationException("Organization is already linked to a different Clerk org.");
        ClerkOrgId = clerkOrgId.Trim();
        Touch();
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Organization name required", nameof(name));
        Name = name.Trim();
        Touch();
    }

    public void SetSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Slug required", nameof(slug));
        Slug = slug.Trim().ToLowerInvariant();
        Touch();
    }
}
