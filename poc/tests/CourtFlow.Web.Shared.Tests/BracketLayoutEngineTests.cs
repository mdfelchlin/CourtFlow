using CourtFlow.ApiClient;
using CourtFlow.Web.Shared.Brackets;

namespace CourtFlow.Web.Shared.Tests;

public sealed class BracketLayoutEngineTests
{
    private static readonly BracketLayoutOptions TestOptions = new(
        CardWidth: 196,
        CardHeight: 56,
        ConnectorGap: 48,
        SectionGap: 96,
        RoundHeaderBandHeight: 28,
        MetaCaptionHeight: 14,
        MatchVerticalGap: 12);

    private static double EffectiveMatchHeight =>
        TestOptions.CardHeight + TestOptions.MetaCaptionHeight;

    private static double MaxAdjacentRoundOneGap =>
        EffectiveMatchHeight + TestOptions.MatchVerticalGap + 2;

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void Layout_single_elimination_positions_standard_tree(int teamCount)
    {
        var nodes = CreateSingleEliminationNodes(teamCount);

        var result = new BracketLayoutEngine().Layout(nodes, TestOptions);

        Assert.Equal(teamCount - 1, result.Nodes.Count);
        Assert.Equal(teamCount - 2, result.Connectors.Count);
        Assert.Equal(result.Nodes.Count, result.Nodes.Select(node => (node.X, node.Y)).Distinct().Count());
        Assert.NotEmpty(result.Junctions);
        Assert.NotEmpty(result.RoundColumns);

        foreach (var connector in result.Connectors)
        {
            var child = Assert.Single(result.Nodes, node => node.Id == connector.FromNodeId);
            var parent = Assert.Single(result.Nodes, node => node.Id == connector.ToNodeId);

            Assert.True(parent.X > child.X);
        }

        foreach (var round in result.Nodes.GroupBy(node => node.Round))
        {
            var x = Assert.Single(round.Select(node => node.X).Distinct());
            Assert.Equal((round.Key - 1) * (TestOptions.CardWidth + TestOptions.ConnectorGap), x);
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void Layout_single_elimination_keeps_round_one_matches_compact(int teamCount)
    {
        var nodes = CreateSingleEliminationNodes(teamCount);
        var result = new BracketLayoutEngine().Layout(nodes, TestOptions);

        var roundOne = result.Nodes
            .Where(node => node.Round == 1)
            .OrderBy(node => node.Slot)
            .ToList();

        for (var i = 1; i < roundOne.Count; i++)
        {
            var gap = roundOne[i].Y - (roundOne[i - 1].Y + roundOne[i - 1].Height);
            Assert.InRange(gap, 0, MaxAdjacentRoundOneGap);
        }
    }

    [Fact]
    public void Layout_eight_team_canvas_stays_compact()
    {
        var nodes = CreateSingleEliminationNodes(teamCount: 8);
        var result = new BracketLayoutEngine().Layout(nodes, TestOptions);

        Assert.True(result.CanvasHeight < 400);
    }

    [Fact]
    public void Layout_sixteen_team_canvas_taller_but_same_leaf_gap()
    {
        var eightTeam = new BracketLayoutEngine().Layout(CreateSingleEliminationNodes(8), TestOptions);
        var sixteenTeam = new BracketLayoutEngine().Layout(CreateSingleEliminationNodes(16), TestOptions);

        Assert.True(sixteenTeam.CanvasHeight > eightTeam.CanvasHeight);

        var eightGap = RoundOneAdjacentGap(eightTeam);
        var sixteenGap = RoundOneAdjacentGap(sixteenTeam);
        Assert.InRange(Math.Abs(eightGap - sixteenGap), 0, 2);
    }

    [Fact]
    public void Layout_single_elimination_parents_center_between_feeders()
    {
        var nodes = CreateSingleEliminationNodes(teamCount: 8);
        var result = new BracketLayoutEngine().Layout(nodes, TestOptions);
        var nodeById = result.Nodes.ToDictionary(node => node.Id);
        var inputsById = nodes.ToDictionary(node => node.Id);

        foreach (var parent in result.Nodes.Where(node => node.Round > 1))
        {
            var feeders = result.Nodes
                .Where(node => inputsById[node.Id].WinsToNodeId == parent.Id)
                .OrderBy(node => node.Slot)
                .ToList();

            if (feeders.Count != 2)
            {
                continue;
            }

            var expectedCenter = (CenterY(feeders[0]) + CenterY(feeders[1])) / 2;
            Assert.InRange(CenterY(parent), expectedCenter - 1, expectedCenter + 1);
        }
    }

    [Fact]
    public void Layout_double_elimination_stacks_winners_losers_and_centers_grand_final()
    {
        var nodes = CreateDoubleEliminationNodes();

        var result = new BracketLayoutEngine().Layout(nodes, TestOptions);

        var winners = result.Sections[BracketBranchDto.Winners];
        var losers = result.Sections[BracketBranchDto.Losers];
        var grandFinal = result.Sections[BracketBranchDto.GrandFinal];

        Assert.Equal(0, winners.Y);
        Assert.Equal("Winners bracket", winners.Title);
        Assert.True(losers.Y > winners.Y);
        Assert.Equal("Losers bracket", losers.Title);
        Assert.True(losers.HasDividerAbove);
        Assert.True(grandFinal.X > winners.Width);

        var winnersFinal = result.Nodes
            .Where(node => node.Branch == BracketBranchDto.Winners)
            .MaxBy(node => node.Round)!;
        var losersFinal = result.Nodes
            .Where(node => node.Branch == BracketBranchDto.Losers)
            .MaxBy(node => node.Round)!;
        var grandFinalNode = Assert.Single(result.Nodes, node => node.Branch == BracketBranchDto.GrandFinal);

        var expectedGrandFinalCenter = (CenterY(winnersFinal) + CenterY(losersFinal)) / 2;
        Assert.InRange(CenterY(grandFinalNode), expectedGrandFinalCenter - 12, expectedGrandFinalCenter + 12);
        Assert.Contains(result.Connectors, connector => connector.Kind == BracketConnectorKind.LosesTo);
    }

    [Fact]
    public void Layout_unknown_branch_creates_additional_section_for_third_place()
    {
        const BracketBranchDto thirdPlace = BracketBranchDto.ThirdPlace;
        var nodes = CreateDoubleEliminationNodes()
            .Concat(new[]
            {
                new BracketLayoutNodeInput(Guid.NewGuid(), thirdPlace, Round: 1, Slot: 0, null, null)
            })
            .ToArray();

        var result = new BracketLayoutEngine().Layout(nodes, TestOptions);

        Assert.True(result.Sections.ContainsKey(thirdPlace));
        Assert.True(result.Sections[thirdPlace].Y > result.Sections[BracketBranchDto.Losers].Y);
    }

    [Fact]
    public void Layout_final_round_column_uses_final_label()
    {
        var nodes = CreateSingleEliminationNodes(teamCount: 8);
        var result = new BracketLayoutEngine().Layout(nodes, TestOptions);

        var finalColumn = Assert.Single(
            result.RoundColumns,
            column => column.Branch == BracketBranchDto.Winners && column.Round == 3);

        Assert.Equal("Final", finalColumn.Label);
    }

    [Fact]
    public void Layout_grand_final_column_uses_grand_final_label()
    {
        var nodes = CreateDoubleEliminationNodes();
        var result = new BracketLayoutEngine().Layout(nodes, TestOptions);

        var grandFinalColumn = Assert.Single(
            result.RoundColumns,
            column => column.Branch == BracketBranchDto.GrandFinal);

        Assert.Equal("Grand final", grandFinalColumn.Label);
    }

    [Fact]
    public void Layout_double_elimination_uses_per_section_round_headers()
    {
        var nodes = CreateDoubleEliminationNodes();
        var result = new BracketLayoutEngine().Layout(nodes, TestOptions);

        var winnersHeaders = result.RoundColumns
            .Where(column => column.Branch == BracketBranchDto.Winners)
            .ToList();
        var losersHeaders = result.RoundColumns
            .Where(column => column.Branch == BracketBranchDto.Losers)
            .ToList();

        Assert.NotEmpty(winnersHeaders);
        Assert.NotEmpty(losersHeaders);
        Assert.All(winnersHeaders, column => Assert.Equal(
            result.Sections[BracketBranchDto.Winners].Y + 24,
            column.HeaderY));
        Assert.All(losersHeaders, column => Assert.Equal(
            result.Sections[BracketBranchDto.Losers].Y + 24,
            column.HeaderY));
        Assert.True(losersHeaders.Min(column => column.HeaderY) > winnersHeaders.Max(column => column.HeaderY));
    }

    private static double RoundOneAdjacentGap(BracketLayoutResult result)
    {
        var roundOne = result.Nodes
            .Where(node => node.Round == 1)
            .OrderBy(node => node.Slot)
            .ToList();

        return roundOne[1].Y - (roundOne[0].Y + roundOne[0].Height);
    }

    private static BracketLayoutNodeInput[] CreateSingleEliminationNodes(int teamCount)
    {
        var rounds = (int)Math.Log2(teamCount);
        var ids = new Dictionary<(int Round, int Slot), Guid>();

        for (var round = 1; round <= rounds; round++)
        {
            var slots = teamCount / (int)Math.Pow(2, round);
            for (var slot = 0; slot < slots; slot++)
            {
                ids[(round, slot)] = Guid.NewGuid();
            }
        }

        return ids
            .OrderBy(pair => pair.Key.Round)
            .ThenBy(pair => pair.Key.Slot)
            .Select(pair =>
            {
                var (round, slot) = pair.Key;
                var winsToNodeId = round == rounds
                    ? (Guid?)null
                    : ids[(round + 1, slot / 2)];

                return new BracketLayoutNodeInput(
                    pair.Value,
                    BracketBranchDto.Winners,
                    round,
                    slot,
                    winsToNodeId,
                    LosesToNodeId: null);
            })
            .ToArray();
    }

    private static BracketLayoutNodeInput[] CreateDoubleEliminationNodes()
    {
        var winnersRoundOneA = Guid.NewGuid();
        var winnersRoundOneB = Guid.NewGuid();
        var winnersFinal = Guid.NewGuid();
        var losersRoundOneA = Guid.NewGuid();
        var losersRoundOneB = Guid.NewGuid();
        var losersFinal = Guid.NewGuid();
        var grandFinal = Guid.NewGuid();

        return
        [
            new(winnersRoundOneA, BracketBranchDto.Winners, Round: 1, Slot: 0, WinsToNodeId: winnersFinal, LosesToNodeId: losersRoundOneA),
            new(winnersRoundOneB, BracketBranchDto.Winners, Round: 1, Slot: 1, WinsToNodeId: winnersFinal, LosesToNodeId: losersRoundOneB),
            new(winnersFinal, BracketBranchDto.Winners, Round: 2, Slot: 0, WinsToNodeId: grandFinal, LosesToNodeId: losersFinal),
            new(losersRoundOneA, BracketBranchDto.Losers, Round: 1, Slot: 0, WinsToNodeId: losersFinal, LosesToNodeId: null),
            new(losersRoundOneB, BracketBranchDto.Losers, Round: 1, Slot: 1, WinsToNodeId: losersFinal, LosesToNodeId: null),
            new(losersFinal, BracketBranchDto.Losers, Round: 2, Slot: 0, WinsToNodeId: grandFinal, LosesToNodeId: null),
            new(grandFinal, BracketBranchDto.GrandFinal, Round: 1, Slot: 0, WinsToNodeId: null, LosesToNodeId: null)
        ];
    }

    private static double CenterY(PositionedBracketNode node) => node.Y + node.Height / 2;
}
