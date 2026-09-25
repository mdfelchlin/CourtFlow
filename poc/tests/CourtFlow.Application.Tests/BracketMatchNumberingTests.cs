using CourtFlow.Application.Features.Brackets;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Application.Tests;

public sealed class BracketMatchNumberingTests
{
    private static readonly Guid TestOrgId = Guid.NewGuid();
    private static readonly Guid TestBracketId = Guid.NewGuid();

    [Fact]
    public void GetSideSourceLabel_returns_winner_of_for_wins_feeder()
    {
        var feeder = Node(BracketBranch.Winners, 1, 0);
        var target = Node(BracketBranch.Winners, 2, 0);
        feeder.WireAdvancement(target.Id, 1, null, null);

        var nodes = new List<BracketNode> { feeder, target };
        var labels = BracketMatchNumbering.BuildLabels(nodes);

        var label = BracketMatchNumbering.GetSideSourceLabel(target, side: 1, nodes, labels);

        Assert.Equal("Winner of M1", label);
    }

    [Fact]
    public void GetSideSourceLabel_returns_loser_of_for_loses_feeder()
    {
        var feeder = Node(BracketBranch.Winners, 1, 0);
        var target = Node(BracketBranch.Losers, 1, 0);
        feeder.WireAdvancement(null, null, target.Id, 1);

        var nodes = new List<BracketNode> { feeder, target };
        var labels = BracketMatchNumbering.BuildLabels(nodes);

        var label = BracketMatchNumbering.GetSideSourceLabel(target, side: 1, nodes, labels);

        Assert.Equal("Loser of M1", label);
    }

    [Fact]
    public void GetSideSourceLabel_returns_open_when_no_feeder()
    {
        var node = Node(BracketBranch.Losers, 1, 0);
        var nodes = new List<BracketNode> { node };
        var labels = BracketMatchNumbering.BuildLabels(nodes);

        var label = BracketMatchNumbering.GetSideSourceLabel(node, side: 1, nodes, labels);

        Assert.Equal("Open", label);
    }

    [Fact]
    public void BuildLabels_double_elimination_graph_never_needs_tbd_for_wired_sides()
    {
        var nodes = CreateEightTeamDoubleEliminationNodes();
        var labels = BracketMatchNumbering.BuildLabels(nodes);

        foreach (var node in nodes)
        {
            for (var side = 1; side <= 2; side++)
            {
                if (node.Branch == BracketBranch.Winners && node.Round == 1)
                {
                    continue;
                }

                var label = BracketMatchNumbering.GetSideSourceLabel(node, side, nodes, labels);
                Assert.NotEqual("TBD", label);
                Assert.True(
                    label.StartsWith("Winner of ", StringComparison.Ordinal)
                    || label.StartsWith("Loser of ", StringComparison.Ordinal),
                    $"Unexpected label '{label}' for {node.Branch} R{node.Round} S{node.Slot} side {side}");
            }
        }
    }

    [Fact]
    public void GetSideSourceLabel_third_place_uses_semifinal_loser_feeders()
    {
        var semiA = Node(BracketBranch.Winners, 2, 0);
        var semiB = Node(BracketBranch.Winners, 2, 1);
        var third = Node(BracketBranch.ThirdPlace, 1, 0);
        WireDrop(semiA, third, 1);
        WireDrop(semiB, third, 2);

        var nodes = new List<BracketNode> { semiA, semiB, third };
        var labels = BracketMatchNumbering.BuildLabels(nodes);

        Assert.Equal("Loser of M1", BracketMatchNumbering.GetSideSourceLabel(third, 1, nodes, labels));
        Assert.Equal("Loser of M2", BracketMatchNumbering.GetSideSourceLabel(third, 2, nodes, labels));
    }

    private static BracketNode Node(BracketBranch branch, int round, int slot) =>
        new(TestOrgId, TestBracketId, branch, round, slot);

    private static List<BracketNode> CreateEightTeamDoubleEliminationNodes()
    {
        var winnersR1 = Enumerable.Range(0, 4).Select(i => Node(BracketBranch.Winners, 1, i)).ToArray();
        var winnersR2 = Enumerable.Range(0, 2).Select(i => Node(BracketBranch.Winners, 2, i)).ToArray();
        var winnersFinal = Node(BracketBranch.Winners, 3, 0);
        var losersR1 = new[] { Node(BracketBranch.Losers, 1, 0), Node(BracketBranch.Losers, 1, 1) };
        var losersFinal = Node(BracketBranch.Losers, 4, 0);
        var grandFinal = Node(BracketBranch.GrandFinal, 1, 0);

        Wire(winnersR1[0], winnersR2[0], 1);
        Wire(winnersR1[1], winnersR2[0], 2);
        Wire(winnersR1[2], winnersR2[1], 1);
        Wire(winnersR1[3], winnersR2[1], 2);
        Wire(winnersR2[0], winnersFinal, 1);
        Wire(winnersR2[1], winnersFinal, 2);
        Wire(winnersFinal, grandFinal, 1);

        WireDrop(winnersR1[0], losersR1[0], 1);
        WireDrop(winnersR1[1], losersR1[0], 2);
        WireDrop(winnersR1[2], losersR1[1], 1);
        WireDrop(winnersR1[3], losersR1[1], 2);
        Wire(losersR1[0], losersFinal, 1);
        Wire(losersR1[1], losersFinal, 2);
        Wire(losersFinal, grandFinal, 2);

        return winnersR1
            .Concat(winnersR2)
            .Append(winnersFinal)
            .Concat(losersR1)
            .Append(losersFinal)
            .Append(grandFinal)
            .ToList();
    }

    private static void Wire(BracketNode from, BracketNode to, int slot) =>
        from.WireAdvancement(to.Id, slot, from.LosesToNodeId, from.LosesToSlot);

    private static void WireDrop(BracketNode from, BracketNode to, int slot) =>
        from.WireAdvancement(from.WinsToNodeId, from.WinsToSlot, to.Id, slot);
}
