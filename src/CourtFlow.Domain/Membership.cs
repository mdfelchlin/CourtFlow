namespace CourtFlow.Domain;

/// <summary>One person in one organization with one role. The person is their WorkOS user ID; the email and name are copied at each sign-in.</summary>
public sealed class Membership
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string UserId { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string Name { get; private set; } = "";
    public OrganizationRole Role { get; private set; }

    private Membership()
    {
    }

    internal Membership(Guid organizationId, string userId, string email, string name, OrganizationRole role)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainRuleException("A member needs a user ID.");

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
        UpdateProfile(email, name);
    }

    internal void SetRole(OrganizationRole role) => Role = role;

    internal void UpdateProfile(string email, string name)
    {
        Email = EmailAddress.Normalize(email);
        Name = (name ?? "").Trim();
    }
}