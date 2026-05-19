using CourtFlow.ApiClient;
using CourtFlow.Web.Shared.Brackets;

namespace CourtFlow.Web.Shared.Tests;

public sealed class MiniBracketLayoutHelperTests
{
    [Fact]
    public void BuildLayoutInputs_lower_group_remaps_sparse_rounds_to_contiguous_columns()
    {
        var roundOne = CreateNode(round: 1, slot: 0);
        var roundTwo = CreateNode(round: 2, slot: 0);
        var roundFour = CreateNode(round: 4, slot: 0);
        roundOne = roundOne with { WinsToNodeId = roundTwo.Id };
        roundTwo = roundTwo with { WinsToNodeId = roundFour.Id };

        var nodes = new[] { roundOne, roundTwo, roundFour };
        var nodeById = nodes.ToDictionary(n => n.Id);
        var group = new BracketDisplayGroupDto(
            "lower",
            1,
            "Lower bracket",
            null,
            BracketDisplayGroupKindDto.Lower,
            nodes.Select(n => n.Id).ToList());

        var inputs = MiniBracketLayoutHelper.BuildLayoutInputs(group, nodeById);
        var result = new BracketLayoutEngine().Layout(inputs, new BracketLayoutOptions(MiniBracket: true));

        var roundOneNode = Assert.Single(result.Nodes, n => n.Id == roundOne.Id);
        var roundTwoNode = Assert.Single(result.Nodes, n => n.Id == roundTwo.Id);
        var roundFourNode = Assert.Single(result.Nodes, n => n.Id == roundFour.Id);

        Assert.Equal(0, roundOneNode.X);
        Assert.True(roundTwoNode.X > roundOneNode.X);
        Assert.True(roundFourNode.X > roundTwoNode.X);
        Assert.Equal(
            (roundFourNode.X - roundTwoNode.X),
            roundTwoNode.X - roundOneNode.X);
    }

    [Fact]
    public void BuildLayoutInputs_lower_group_preserves_wins_to_connectors()
    {
        var roundOne = CreateNode(round: 1, slot: 0);
        var roundTwo = CreateNode(round: 2, slot: 0);
        roundOne = roundOne with { WinsToNodeId = roundTwo.Id };

        var nodes = new[] { roundOne, roundTwo };
        var nodeById = nodes.ToDictionary(n => n.Id);
        var group = new BracketDisplayGroupDto(
            "lower",
            1,
            "Lower bracket",
            null,
            BracketDisplayGroupKindDto.Lower,
            nodes.Select(n => n.Id).ToList());

        var inputs = MiniBracketLayoutHelper.BuildLayoutInputs(group, nodeById);
        var result = new BracketLayoutEngine().Layout(inputs, new BracketLayoutOptions(MiniBracket: true));

        Assert.NotEmpty(result.Connectors);
        Assert.All(result.Connectors, connector => Assert.Equal(BracketConnectorKind.WinsTo, connector.Kind));
    }

    [Fact]
    public void BuildContiguousRoundMap_maps_distinct_rounds_in_order()
    {
        var nodes = new[]
        {
            CreateNode(round: 1, slot: 0),
            CreateNode(round: 2, slot: 0),
            CreateNode(round: 4, slot: 0)
        };

        var map = MiniBracketLayoutHelper.BuildContiguousRoundMap(nodes);

        Assert.Equal(1, map[1]);
        Assert.Equal(2, map[2]);
        Assert.Equal(3, map[4]);
    }

    private static BracketTreeNodeDto CreateNode(int round, int slot)
    {
        var id = Guid.NewGuid();
        return new BracketTreeNodeDto(
            id,
            Guid.NewGuid(),
            BracketBranchDto.Losers,
            round,
            slot,
            MatchId: null,
            HomeTeamId: null,
            AwayTeamId: null,
            HomeSeed: null,
            AwaySeed: null,
            WinsToNodeId: null,
            LosesToNodeId: null,
            HomeLabel: "TBD",
            AwayLabel: "TBD",
            HomeIsBye: false,
            AwayIsBye: false,
            IsPinnedHome: false,
            IsPinnedAway: false,
            MatchState: null,
            Sets: Array.Empty<BracketTreeSetScoreDto>(),
            HomeSetsSummary: null,
            AwaySetsSummary: null,
            WinnerTeamId: null,
            WinnerSide: null,
            CourtName: null,
            ScheduledStartUtc: null);
    }
}
