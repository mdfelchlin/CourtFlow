namespace CourtFlow.Application.Seeding;

/// <summary>
/// Standard single-elimination seed placement for bracket slot positions.
/// Consecutive pairs form round-one matchups (1vN, etc.).
/// </summary>
public static class BracketSeedOrder
{
    /// <summary>
    /// Returns seed numbers (1-based ranks) for each bracket slot position, in pair order.
    /// Length equals <paramref name="bracketSize"/> (typically next power of two ≥ team count).
    /// </summary>
    public static IReadOnlyList<int> BuildSlotSeedNumbers(int bracketSize)
    {
        if (bracketSize < 2)
            throw new ArgumentOutOfRangeException(nameof(bracketSize), "Bracket size must be at least 2.");

        return BuildRecursive(bracketSize);
    }

    private static List<int> BuildRecursive(int size)
    {
        if (size == 2)
            return [1, 2];

        var half = BuildRecursive(size / 2);
        var result = new List<int>(size);
        foreach (var seed in half)
        {
            result.Add(seed);
            result.Add(size + 1 - seed);
        }

        return result;
    }

    public static int NextPowerOfTwo(int teamCount)
    {
        var p = 1;
        while (p < teamCount) p <<= 1;
        return Math.Max(2, p);
    }
}
