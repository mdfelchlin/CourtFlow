namespace CourtFlow.Web.Shared.Common;

public static class TeamDisplay
{
    public static string Label(string? club, string name) =>
        string.IsNullOrWhiteSpace(club) ? name : $"{club.Trim()} {name.Trim()}";
}
