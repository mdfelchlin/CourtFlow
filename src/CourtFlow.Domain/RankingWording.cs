namespace CourtFlow.Domain;

/// <summary>The words the Format page and the ranking step share, so the two cannot drift.</summary>
public static class RankingWording
{
    public const string NotRanked = "Not ranked";
    public const string Ranked = "Ranked";
    public const string AddTeamsFirst = "Add teams first.";
    public const string NameOrderAlert = "Teams are shown in name order. Save to keep this order, or change it first.";
    public const string PoolsLater = "Pools are coming later.";
    public const string BracketsLater = "Brackets are coming later.";
    public const string DivisionNotFound = "This division was not found.";
    public const string SelectTournament = "Select a tournament to see its divisions.";
    public const string AddDivisionFirst = "Add a division to this tournament before ranking teams.";
    public const string RankingRandomized = "Ranking randomized.";
    public const string DiscardTitle = "Discard ranking changes?";
    public const string DiscardBody = "Your changes to this ranking have not been saved.";
    public const string DiscardConfirm = "Discard";
    public const string KeepEditing = "Keep editing";
    public const string BackToFormat = "Back to format";

    public static string Status(int teamCount, bool ranked, int newTeams)
    {
        if (teamCount == 0)
            return AddTeamsFirst;
        if (!ranked)
            return NotRanked;
        if (newTeams <= 0)
            return Ranked;

        return newTeams == 1
            ? "Ranked, 1 new team at the bottom."
            : $"Ranked, {newTeams} new teams at the bottom.";
    }

    public static string TeamCount(int count) => count == 1 ? "1 team." : $"{count} teams.";

    public static string RankRejected(int count) => $"Enter a rank from 1 to {count},";

    public static string Moved(string teamName, int rank, int total) =>
        $"{teamName} moved to rank {rank} of {total}.";

    public static string EditLabel(string divisionLabel) => $"Edit {divisionLabel}";

    public static string ViewLabel(string divisionLabel) => $"View {divisionLabel}";

    public static string RankOf(string teamName, string clubName) => $"Rank of {teamName}, {clubName}";

    public static string DragOf(string teamName, string clubName) => $"Drag {teamName}, {clubName}";
}
