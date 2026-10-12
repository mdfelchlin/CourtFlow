namespace CourtFlow.Domain;

public sealed class Team
{
    public const string NameRequired = "Team name is required.";
    public const string DuplicateName = "Another team in this club already has this name.";

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ClubId { get; private set; }
    public string Name { get; private set; } = "";
    public string NormalizedName { get; private set; } = "";

    private Team()
    {
    }

    internal Team(Guid organizationId, Guid clubId, string name)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ClubId = clubId;
        Rename(name);
    }

    public static void Validate(string? name) => RequireName(name);

    internal void Rename(string name)
    {
        Name = RequireName(name);
        NormalizedName = Club.Normalize(Name);
    }

    internal static string RequireName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException(NameRequired);

        return name.Trim();
    }
}
