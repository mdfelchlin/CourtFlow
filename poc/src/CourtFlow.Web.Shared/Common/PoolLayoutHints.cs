namespace CourtFlow.Web.Shared.Common;

/// <summary>
/// Client-side pool layout hints (mirrors <c>PoolSeeding.BuildPoolSizes</c> in Application).
/// </summary>
public static class PoolLayoutHints
{
    public static IReadOnlyList<int> BuildPoolSizes(int teamCount)
    {
        if (teamCount < 3) return Array.Empty<int>();
        if (teamCount == 5) return new[] { 5 };

        for (var poolsOfFour = teamCount / 4; poolsOfFour >= 0; poolsOfFour--)
        {
            var remainder = teamCount - (poolsOfFour * 4);
            if (remainder % 3 != 0) continue;
            var poolsOfThree = remainder / 3;
            var result = new List<int>(poolsOfFour + poolsOfThree);
            for (var i = 0; i < poolsOfFour; i++) result.Add(4);
            for (var i = 0; i < poolsOfThree; i++) result.Add(3);
            return result;
        }

        return Array.Empty<int>();
    }

    public static string FormatSuggestedLayout(int teamCount)
    {
        var sizes = BuildPoolSizes(teamCount);
        if (sizes.Count == 0)
            return $"At least 3 teams are required (you have {teamCount}).";
        var parts = sizes.Select((s, i) => $"Pool {(char)('A' + i)}: {s} teams");
        return $"{sizes.Count} pool(s) for {teamCount} teams — {string.Join(", ", parts)}.";
    }
}
