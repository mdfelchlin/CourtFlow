using CourtFlow.Web;

namespace CourtFlow.Web.Tests;

public class RankingOrderTests
{
    [Fact]
    public void MoveMatchesTakingARowOutAndInsertingIt()
    {
        var items = new[] { "Alpha", "Bravo", "Charlie", "Delta" };

        Assert.Equal(["Charlie", "Alpha", "Bravo", "Delta"], RankingOrder.Move(items, 2, 0));
        Assert.Equal(["Alpha", "Charlie", "Bravo", "Delta"], RankingOrder.Move(items, 2, 1));
        Assert.Equal(["Alpha", "Bravo", "Delta", "Charlie"], RankingOrder.Move(items, 2, 3));
        Assert.Equal(items, RankingOrder.Move(items, 2, 2));
    }

    [Fact]
    public void ShuffleUsesTheRandomSourceAndNeverKeepsTheCurrentOrder()
    {
        var items = new[] { "Alpha", "Bravo", "Charlie" };

        var shuffled = RankingOrder.Shuffle(items, new FixedRandom(0));
        Assert.NotEqual(items, shuffled);
        Assert.Equal(RankingOrder.Shuffle(items, new FixedRandom(0)), shuffled);

        var forced = RankingOrder.Shuffle(items, new FixedRandom(int.MaxValue));
        Assert.Equal(["Bravo", "Alpha", "Charlie"], forced);
    }

    private sealed class FixedRandom(int value) : IRankingRandom
    {
        public int NextValue(int maxExclusive)
        {
            var chosen = value < 0 ? 0 : value;
            return chosen >= maxExclusive ? maxExclusive - 1 : chosen;
        }
    }
}