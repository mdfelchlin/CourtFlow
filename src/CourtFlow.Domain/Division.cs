namespace CourtFlow.Domain;

public enum DivisionGender
{
    Female,
    Male,
}

public sealed class Division
{
    public const string NameRequired = "Division name is required.";
    public const string GenderRequired = "Gender is required.";
    public const string DuplicateNameAndGender = "A division with this name and gender already exists.";

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid TournamentId { get; private set; }
    public string Name { get; private set; } = "";
    public DivisionGender Gender { get; private set; }

    private Division()
    {
    }

    internal Division(Guid organizationId, Guid tournamentId, string? name, string? gender)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        TournamentId = tournamentId;
        Rename(name);
        SetGender(gender);
    }

    public void Rename(string? name) => Name = RequireName(name);

    public void SetGender(string? gender) => Gender = RequireGender(gender);

    public static void Validate(string? name, string? gender)
    {
        RequireName(name);
        RequireGender(gender);
    }

    public static void EnsureUnique(
        string? name,
        string? gender,
        IEnumerable<(Guid Id, string Name, string Gender)> existing,
        Guid? exceptId)
    {
        var trimmed = RequireName(name);
        var parsed = RequireGender(gender);
        var genderName = parsed.ToString();
        if (existing.Any(item =>
                item.Id != exceptId
                && item.Gender == genderName
                && string.Equals(item.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            throw new DomainRuleException(DuplicateNameAndGender);
    }

    public static IReadOnlyList<T> OrderForList<T>(IEnumerable<T> items, Func<T, string> name, Func<T, string> gender) =>
        items
            .OrderBy(item => name(item), StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => gender(item), StringComparer.Ordinal)
            .ToList();

    internal static string RequireName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException(NameRequired);

        return name.Trim();
    }

    internal static DivisionGender RequireGender(string? gender)
    {
        if (gender == nameof(DivisionGender.Female))
            return DivisionGender.Female;
        if (gender == nameof(DivisionGender.Male))
            return DivisionGender.Male;

        throw new DomainRuleException(GenderRequired);
    }
}
