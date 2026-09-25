namespace CourtFlow.Application.Seeding;

/// <summary>
/// Shared pool size and snake-assignment helpers for ranked imports and division seeding UI.
/// </summary>
public static class PoolSeeding
{
    public static int GetSnakePoolIndex(int position, int poolCount)
    {
        var block = position / poolCount;
        var offset = position % poolCount;
        return block % 2 == 0 ? offset : poolCount - 1 - offset;
    }

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

    public static IReadOnlyList<int> BuildSnakePoolSequence(IReadOnlyList<int> poolSizes)
    {
        var remaining = poolSizes.ToArray();
        var sequence = new List<int>(remaining.Sum());

        while (remaining.Any(x => x > 0))
        {
            for (var i = 0; i < remaining.Length; i++)
            {
                if (remaining[i] <= 0) continue;
                sequence.Add(i);
                remaining[i]--;
            }
            for (var i = remaining.Length - 1; i >= 0; i--)
            {
                if (remaining[i] <= 0) continue;
                sequence.Add(i);
                remaining[i]--;
            }
        }

        return sequence;
    }
}
