namespace CourtFlow.Web;

public sealed class RankingRow
{
    public RankingRow(Guid entryId, string clubName, string teamName, bool isNew)
    {
        EntryId = entryId;
        ClubName = clubName;
        TeamName = teamName;
        IsNew = isNew;
    }

    public Guid EntryId { get; }
    public string ClubName { get; }
    public string TeamName { get; }
    public bool IsNew { get; }
}


/// <summary>The random source a ranking shuffle draws from. Tests substitute a scripted one.</summary>
public interface IRankingRandom
{
    /// <summary>A value from 0 inclusive to <paramref name="maxExclusive"/> exclusive.</summary>
    int NextValue(int maxExclusive);
}

public sealed class SystemRankingRandom : IRankingRandom
{
    public int NextValue(int maxExclusive) => Random.Shared.Next(maxExclusive);
}

/// <summary>Reorders the on-screen ranking. Dragging and typing a rank both go through <see cref="Move"/>.</summary>
public static class RankingOrder
{
    public static List<T> Move<T>(IReadOnlyList<T> items, int from, int to)
    {
        var next = items.ToList();
        if (from == to || from < 0 || to < 0 || from >= next.Count || to >= next.Count)
            return next;

        var item = next[from];
        next.RemoveAt(from);
        next.Insert(to, item);
        return next;
    }

    /// <summary>
    /// A uniform shuffle of every order except the one on screen. A broken random source that keeps
    /// returning the current order is swapped once so the press still changes something.
    /// </summary>
    public static List<T> Shuffle<T>(IReadOnlyList<T> items, IRankingRandom random)
    {
        if (items.Count < 2)
            return items.ToList();

        var current = items.ToList();
        List<T> next = current;
        for (var attempt = 0; attempt < 16 && Same(next, current); attempt++)
        {
            next = current.ToList();
            for (var index = next.Count - 1; index > 0; index--)
            {
                var swap = random.NextValue(index + 1);
                if ((uint)swap > (uint)index)
                    swap = index;
                (next[index], next[swap]) = (next[swap], next[index]);
            }
        }

        if (Same(next, current))
            (next[0], next[1]) = (next[1], next[0]);

        return next;
    }

    public static bool Same<T>(IReadOnlyList<T> left, IReadOnlyList<T> right) =>
        left.Count == right.Count && left.Zip(right).All(pair => EqualityComparer<T>.Default.Equals(pair.First, pair.Second));
}