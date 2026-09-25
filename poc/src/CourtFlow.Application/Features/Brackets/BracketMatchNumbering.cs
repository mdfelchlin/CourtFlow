using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Application.Features.Brackets;

internal static class BracketMatchNumbering
{
    public static Dictionary<Guid, string> BuildLabels(IEnumerable<BracketNode> nodes)
    {
        var result = new Dictionary<Guid, string>();
        var index = 1;
        foreach (var node in OrderForNumbering(nodes))
        {
            result[node.Id] = $"M{index++}";
        }

        return result;
    }

    public static IEnumerable<BracketNode> OrderForNumbering(IEnumerable<BracketNode> nodes) =>
        nodes.OrderBy(BranchOrder)
            .ThenBy(n => n.Round)
            .ThenBy(n => n.Slot);

    public static string GetSideSourceLabel(
        BracketNode node,
        int side,
        IReadOnlyList<BracketNode> nodes,
        IReadOnlyDictionary<Guid, string> matchLabels)
    {
        var feederId = GetFeederNodeId(nodes, node.Id, side);
        if (feederId is { } fid && matchLabels.TryGetValue(fid, out var feederLabel))
        {
            return $"Winner of {feederLabel}";
        }

        var loseFeederId = GetLoseFeederNodeId(nodes, node.Id, side);
        if (loseFeederId is { } loseFid && matchLabels.TryGetValue(loseFid, out var loseFeederLabel))
        {
            return $"Loser of {loseFeederLabel}";
        }

        return "Open";
    }

    private static int BranchOrder(BracketNode node) => node.Branch switch
    {
        BracketBranch.Winners => 0,
        BracketBranch.Losers => 1,
        BracketBranch.GrandFinal => 2,
        BracketBranch.ThirdPlace => 3,
        _ => 4
    };

    public static Guid? GetFeederNodeId(
        IReadOnlyList<BracketNode> nodes,
        Guid targetNodeId,
        int side)
    {
        var feeders = nodes
            .Where(n => n.WinsToNodeId == targetNodeId)
            .OrderBy(n => n.Slot)
            .ToList();

        if (feeders.Count == 0)
        {
            return null;
        }

        if (feeders.Count == 1)
        {
            return feeders[0].Id;
        }

        return side == 1 ? feeders[0].Id : feeders[1].Id;
    }

    public static Guid? GetLoseFeederNodeId(
        IReadOnlyList<BracketNode> nodes,
        Guid targetNodeId,
        int side)
    {
        var feeders = nodes
            .Where(n => n.LosesToNodeId == targetNodeId && (n.LosesToSlot ?? 2) == side)
            .OrderBy(n => n.Slot)
            .ToList();

        if (feeders.Count == 0)
        {
            return null;
        }

        return feeders[0].Id;
    }
}
