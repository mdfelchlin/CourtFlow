using CourtFlow.ApiClient;

namespace CourtFlow.Web.Shared.Brackets;

public static class MiniBracketLayoutHelper
{
    public static IReadOnlyList<BracketLayoutNodeInput> BuildLayoutInputs(
        BracketDisplayGroupDto group,
        IReadOnlyDictionary<Guid, BracketTreeNodeDto> nodeById)
    {
        var groupIdSet = group.NodeIds.ToHashSet();
        var groupNodes = group.NodeIds
            .Select(id => nodeById[id])
            .ToList();

        var roundMap = BuildContiguousRoundMap(groupNodes);
        var remapRounds = group.Kind == BracketDisplayGroupKindDto.Lower;

        return groupNodes
            .Select(n => new BracketLayoutNodeInput(
                n.Id,
                BracketBranchDto.Winners,
                remapRounds ? roundMap[n.Round] : n.Round,
                n.Slot,
                n.WinsToNodeId is { } winsTo && groupIdSet.Contains(winsTo) ? winsTo : null,
                LosesToNodeId: null))
            .ToList();
    }

    public static Dictionary<int, int> BuildContiguousRoundMap(IReadOnlyList<BracketTreeNodeDto> nodes)
    {
        var map = new Dictionary<int, int>();
        var displayRound = 1;
        foreach (var round in nodes.Select(n => n.Round).Distinct().Order())
        {
            map[round] = displayRound++;
        }

        return map;
    }
}
