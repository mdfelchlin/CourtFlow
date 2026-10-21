namespace CourtFlow.Domain;

public static class TournamentWarnings
{
    public static int InclusiveDays(DateOnly start, DateOnly end) =>
        end.DayNumber - start.DayNumber + 1;

    public static bool SpanExceedsFiveDays(DateOnly start, DateOnly end) =>
        InclusiveDays(start, end) > 5;

    public static bool NameMatchesAnother(string? name, IEnumerable<string> otherNames)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var normalized = name.Trim();
        return otherNames.Any(other =>
            string.Equals(other.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
    }
}