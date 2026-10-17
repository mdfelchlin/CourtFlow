namespace CourtFlow.Domain;

public sealed class Organization
{
    public const int NameMaxLength = 100;
    public const string NeedsAnOwner = "An organization needs at least one owner.";
    public const string AlreadyAMember = "This person is already a member.";
    public const string MemberNotFound = "Member was not found.";
    public const string AlreadyDeactivated = "This organization is already deactivated.";
    public const string AlreadyActive = "This organization is already active.";

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public OrganizationStatus Status { get; private set; } = OrganizationStatus.Active;

    private readonly List<Membership> _memberships = [];

    public IReadOnlyCollection<Membership> Memberships => _memberships;

    public bool IsActive => Status == OrganizationStatus.Active;

    /// <summary>Whether its published tournaments may appear on the public site. The public slice reads this.</summary>
    public bool IsVisibleToPublic => IsActive;

    private Organization()
    {
    }

    public Organization(Guid id, string name)
    {
        Id = id;
        Name = RequireName(name);
    }

    public static Organization Create(string? name) => new(Guid.NewGuid(), name!);

    public static string RequireName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException("Organization name is required.");

        var trimmed = name.Trim();
        if (trimmed.Length > NameMaxLength)
            throw new DomainRuleException($"Organization name must be {NameMaxLength} characters or fewer.");

        return trimmed;
    }

    public Membership? MemberFor(string userId) => _memberships.FirstOrDefault(member => member.UserId == userId);

    public Membership? MemberWithEmail(string email)
    {
        var normalized = EmailAddress.Normalize(email);
        return _memberships.FirstOrDefault(member => member.Email == normalized);
    }

    public Membership AddMember(string userId, string email, string name, OrganizationRole role)
    {
        if (MemberFor(userId) is not null)
            throw new DomainRuleException(AlreadyAMember);

        var membership = new Membership(Id, userId, email, name, role);
        _memberships.Add(membership);
        return membership;
    }

    public void ChangeRole(Guid membershipId, OrganizationRole role)
    {
        var membership = Find(membershipId);
        if (membership.Role == OrganizationRole.Owner && role != OrganizationRole.Owner)
            EnsureAnotherOwner(membership);

        membership.SetRole(role);
    }

    public void RemoveMember(Guid membershipId)
    {
        var membership = Find(membershipId);
        if (membership.Role == OrganizationRole.Owner)
            EnsureAnotherOwner(membership);

        _memberships.Remove(membership);
    }

    public void UpdateMemberProfile(string userId, string email, string name) =>
        MemberFor(userId)?.UpdateProfile(email, name);

    public bool IsLastOwner(Membership membership) =>
        membership.Role == OrganizationRole.Owner
        && _memberships.Count(member => member.Role == OrganizationRole.Owner) == 1;

    public void Deactivate()
    {
        if (Status == OrganizationStatus.Deactivated)
            throw new DomainRuleException(AlreadyDeactivated);

        Status = OrganizationStatus.Deactivated;
    }

    public void Reactivate()
    {
        if (Status == OrganizationStatus.Active)
            throw new DomainRuleException(AlreadyActive);

        Status = OrganizationStatus.Active;
    }

    private Membership Find(Guid membershipId) =>
        _memberships.FirstOrDefault(member => member.Id == membershipId)
            ?? throw new DomainRuleException(MemberNotFound);

    private void EnsureAnotherOwner(Membership leaving)
    {
        if (!_memberships.Any(member => member.Id != leaving.Id && member.Role == OrganizationRole.Owner))
            throw new DomainRuleException(NeedsAnOwner);
    }
}
