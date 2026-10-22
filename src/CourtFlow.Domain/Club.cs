namespace CourtFlow.Domain;

public sealed class Club
{
    public const string NameRequired = "Club is required.";

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = "";
    public string NormalizedName { get; private set; } = "";

    private readonly List<Team> _teams = [];

    public IReadOnlyCollection<Team> Teams => _teams;

    private Club()
    {
    }

    public static Club Create(Guid organizationId, string? name)
    {
        var trimmed = RequireName(name);
        return new Club
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = trimmed,
            NormalizedName = Normalize(trimmed),
        };
    }

    public static void Validate(string? name) => RequireName(name);

    /// <summary>The comparison key for club and team names: trimmed and upper-cased. The unique indexes use it too.</summary>
    public static string Normalize(string name) => name.Trim().ToUpperInvariant();

    public Team? FindTeam(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : _teams.FirstOrDefault(team => team.NormalizedName == Normalize(name));

    public Team AddTeam(string? name)
    {
        var trimmed = Team.RequireName(name);
        if (FindTeam(trimmed) is not null)
            throw new DomainRuleException(Team.DuplicateName);

        var team = new Team(OrganizationId, Id, trimmed);
        _teams.Add(team);
        return team;
    }

    public Team RenameTeam(Guid teamId, string? name)
    {
        var team = _teams.FirstOrDefault(item => item.Id == teamId)
            ?? throw new DomainRuleException("Team was not found.");
        var trimmed = Team.RequireName(name);
        if (FindTeam(trimmed) is { } other && other.Id != teamId)
            throw new DomainRuleException(Team.DuplicateName);

        team.Rename(trimmed);
        return team;
    }

    private static string RequireName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException(NameRequired);

        return name.Trim();
    }
}