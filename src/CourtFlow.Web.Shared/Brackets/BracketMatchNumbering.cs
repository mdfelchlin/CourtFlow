using CourtFlow.ApiClient;

namespace CourtFlow.Web.Shared.Brackets;

public static class BracketMatchNumbering
{
    public static Dictionary<Guid, string> BuildLabels(IEnumerable<BracketTreeNodeDto> nodes)
    {
        var result = new Dictionary<Guid, string>();
        var index = 1;
        foreach (var node in OrderForNumbering(nodes))
        {
            result[node.Id] = $"M{index++}";
        }

        return result;
    }

    public static IEnumerable<BracketTreeNodeDto> OrderForNumbering(IEnumerable<BracketTreeNodeDto> nodes) =>
        nodes.OrderBy(BranchOrder)
            .ThenBy(n => n.Round)
            .ThenBy(n => n.Slot);

    private static int BranchOrder(BracketTreeNodeDto node) => node.Branch switch
    {
        BracketBranchDto.Winners => 0,
        BracketBranchDto.Losers => 1,
        BracketBranchDto.GrandFinal => 2,
        BracketBranchDto.ThirdPlace => 3,
        _ => 4
    };

    public static Guid? GetFeederNodeId(
        IReadOnlyList<BracketTreeNodeDto> nodes,
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
}
