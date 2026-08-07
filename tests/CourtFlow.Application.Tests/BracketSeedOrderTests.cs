using CourtFlow.Application.Seeding;

namespace CourtFlow.Application.Tests;

public class BracketSeedOrderTests
{
    [Theory]
    [InlineData(2, new[] { 1, 2 })]
    [InlineData(4, new[] { 1, 4, 2, 3 })]
    [InlineData(8, new[] { 1, 8, 4, 5, 2, 7, 3, 6 })]
    public void BuildSlotSeedNumbers_uses_standard_pairing(int size, int[] expected)
    {
        var result = BracketSeedOrder.BuildSlotSeedNumbers(size);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(6, 8)]
    [InlineData(8, 8)]
    [InlineData(9, 16)]
    public void NextPowerOfTwo(int teamCount, int expected)
    {
        Assert.Equal(expected, BracketSeedOrder.NextPowerOfTwo(teamCount));
    }
}
