using CourtFlow.Application.Features.Brackets;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Application.Tests;

public sealed class BracketDisplayGrouperTests
{
    private static readonly Guid TestOrgId = Guid.NewGuid();
    private static readonly Guid TestBracketId = Guid.NewGuid();

    [Fact]
    public void BuildGroups_single_elimination_produces_one_main_group()
    {
        var nodes = CreateSingleEliminationNodes(8);

        var groups = BracketDisplayGrouper.BuildGroups(nodes);

        var main = Assert.Single(groups);
        Assert.Equal(BracketDisplayGroupKind.Main, main.Kind);
        Assert.Equal("Main bracket", main.Title);
        Assert.Equal(7, main.NodeIds.Count);
    }

    [Fact]
    public void BuildGroups_double_elimination_orders_main_lower_grand_final()
    {
        var nodes = CreateDoubleEliminationNodes();

        var groups = BracketDisplayGrouper.BuildGroups(nodes);

        Assert.Equal(4, groups.Count);
        Assert.Equal(BracketDisplayGroupKind.Main, groups[0].Kind);
        Assert.Equal(BracketDisplayGroupKind.Lower, groups[1].Kind);
        Assert.Equal("Lower bracket", groups[1].Title);
        Assert.Equal(3, groups[1].NodeIds.Count);
        Assert.Equal(BracketDisplayGroupKind.SingleMatch, groups[2].Kind);
        Assert.Equal(BracketDisplayGroupKind.ThirdPlace, groups[3].Kind);
        Assert.Equal(0, groups[0].Sequence);
        Assert.True(groups[1].Sequence < groups[2].Sequence);
    }

    [Fact]
    public void BuildGroups_includes_all_losers_nodes_in_one_lower_group()
    {
        var winnersA = Node(BracketBranch.Winners, 1, 0);
        var winnersB = Node(BracketBranch.Winners, 1, 1);
        var winnersFinal = Node(BracketBranch.Winners, 2, 0);
        var losersA = Node(BracketBranch.Losers, 1, 0);
        var losersB = Node(BracketBranch.Losers, 1, 1);
        var losersFinal = Node(BracketBranch.Losers, 2, 0);
        var orphan = Node(BracketBranch.Losers, 4, 0);
        var grandFinal = Node(BracketBranch.GrandFinal, 1, 0);

        Wire(winnersA, winnersFinal, slot: 1);
        Wire(winnersB, winnersFinal, slot: 2);
        Wire(winnersFinal, grandFinal, slot: 1);
        Wire(losersA, losersFinal, slot: 1);
        Wire(losersB, losersFinal, slot: 2);
        Wire(losersFinal, grandFinal, slot: 2);

        var nodes = new List<BracketNode>
        {
            winnersA, winnersB, winnersFinal,
            losersA, losersB, losersFinal,
            orphan,
            grandFinal
        };

        var groups = BracketDisplayGrouper.BuildGroups(nodes);

        var lower = Assert.Single(groups, g => g.Kind == BracketDisplayGroupKind.Lower);
        Assert.Equal(4, lower.NodeIds.Count);
        Assert.Contains(orphan.Id, lower.NodeIds);
        Assert.DoesNotContain(groups, g => g.Kind == BracketDisplayGroupKind.SingleMatch && g.NodeIds.Contains(orphan.Id));
    }

    private static BracketNode Node(BracketBranch branch, int round, int slot) =>
        new(TestOrgId, TestBracketId, branch, round, slot);

    private static void Wire(BracketNode from, BracketNode to, int slot)
    {
        from.WireAdvancement(to.Id, slot, null, null);
    }

    private static List<BracketNode> CreateSingleEliminationNodes(int teamCount)
    {
        var rounds = (int)Math.Log2(teamCount);
        var ids = new Dictionary<(int Round, int Slot), BracketNode>();

        for (var round = 1; round <= rounds; round++)
        {
            var slots = teamCount / (int)Math.Pow(2, round);
            for (var slot = 0; slot < slots; slot++)
            {
                ids[(round, slot)] = Node(BracketBranch.Winners, round, slot);
            }
        }

        foreach (var (key, node) in ids)
        {
            if (key.Round == rounds) continue;
            var target = ids[(key.Round + 1, key.Slot / 2)];
            node.WireAdvancement(target.Id, key.Slot % 2 == 0 ? 1 : 2, null, null);
        }

        return ids.Values.ToList();
    }

    private static List<BracketNode> CreateDoubleEliminationNodes()
    {
        var winnersRoundOneA = Node(BracketBranch.Winners, 1, 0);
        var winnersRoundOneB = Node(BracketBranch.Winners, 1, 1);
        var winnersFinal = Node(BracketBranch.Winners, 2, 0);
        var losersRoundOneA = Node(BracketBranch.Losers, 1, 0);
        var losersRoundOneB = Node(BracketBranch.Losers, 1, 1);
        var losersFinal = Node(BracketBranch.Losers, 2, 0);
        var grandFinal = Node(BracketBranch.GrandFinal, 1, 0);
        var thirdPlace = Node(BracketBranch.ThirdPlace, 1, 0);

        Wire(winnersRoundOneA, winnersFinal, 1);
        Wire(winnersRoundOneB, winnersFinal, 2);
        Wire(winnersFinal, grandFinal, 1);
        Wire(losersRoundOneA, losersFinal, 1);
        Wire(losersRoundOneB, losersFinal, 2);
        Wire(losersFinal, grandFinal, 2);

        return
        [
            winnersRoundOneA, winnersRoundOneB, winnersFinal,
            losersRoundOneA, losersRoundOneB, losersFinal,
            grandFinal, thirdPlace
        ];
    }
}
