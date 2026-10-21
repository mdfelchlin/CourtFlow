using CourtFlow.Web;

namespace CourtFlow.Web.Tests;

public class RankingOrderTests
{
    [Fact]
    public void Move_matches_taking_a_row_out_and_inserting_it()
    {
        var items = new[] { "Alpha", "Bravo", "Charlie", "Delta" };

        Assert.Equal(["Charlie", "Alpha", "Bravo", "Delta"], RankingOrder.Move(items, 2, 0));
        Assert.Equal(["Alpha", "Charlie", "Bravo", "Delta"], RankingOrder.Move(items, 2, 1));
        Assert.Equal(["Alpha", "Bravo", "Delta", "Charlie"], RankingOrder.Move(items, 2, 3));
        Assert.Equal(items, RankingOrder.Move(items, 2, 2));
    }

    [Fact]
    public void Shuffle_uses_the_random_source_and_never_keeps_the_current_order()
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
        public int Next(int maxExclusive)
        {
            var chosen = value < 0 ? 0 : value;
            return chosen >= maxExclusive ? maxExclusive - 1 : chosen;
        }
    }
}
