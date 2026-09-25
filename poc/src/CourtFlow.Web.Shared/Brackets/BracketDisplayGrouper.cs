using CourtFlow.ApiClient;

namespace CourtFlow.Web.Shared.Brackets;

public static class BracketDisplayGrouper
{
    public static IReadOnlyList<BracketDisplayGroupDto> BuildGroups(IReadOnlyList<BracketTreeNodeDto> nodes)
    {
        if (nodes.Count == 0)
        {
            return Array.Empty<BracketDisplayGroupDto>();
        }

        var groups = new List<BracketDisplayGroupDto>();
        var sequence = 0;

        var winners = nodes
            .Where(n => n.Branch == BracketBranchDto.Winners)
            .OrderBy(n => n.Round)
            .ThenBy(n => n.Slot)
            .ToList();

        if (winners.Count > 0)
        {
            groups.Add(CreateGroup(
                "main",
                sequence++,
                "Main bracket",
                null,
                BracketDisplayGroupKindDto.Main,
                winners.Select(n => n.Id).ToList()));
        }
        else if (!nodes.Any(n => n.Branch == BracketBranchDto.Losers))
        {
            groups.Add(CreateGroup(
                "main",
                sequence++,
                "Main bracket",
                null,
                BracketDisplayGroupKindDto.Main,
                nodes.Select(n => n.Id).ToList()));
            return groups;
        }

        if (TryCreateLowerGroup(nodes, out var lowerGroup))
        {
            groups.Add(lowerGroup with { Sequence = sequence++ });
        }

        var grandFinal = nodes
            .Where(n => n.Branch == BracketBranchDto.GrandFinal)
            .OrderBy(n => n.Round)
            .ThenBy(n => n.Slot)
            .Select(n => n.Id)
            .ToList();

        if (grandFinal.Count > 0)
        {
            groups.Add(CreateGroup(
                "grand-final",
                sequence++,
                "Grand final",
                null,
                grandFinal.Count == 1 ? BracketDisplayGroupKindDto.SingleMatch : BracketDisplayGroupKindDto.GrandFinal,
                grandFinal));
        }

        var thirdPlace = nodes
            .Where(n => n.Branch == BracketBranchDto.ThirdPlace)
            .OrderBy(n => n.Round)
            .ThenBy(n => n.Slot)
            .Select(n => n.Id)
            .ToList();

        if (thirdPlace.Count > 0)
        {
            groups.Add(CreateGroup(
                "third-place",
                sequence++,
                "Third place",
                null,
                BracketDisplayGroupKindDto.ThirdPlace,
                thirdPlace));
        }

        return groups;
    }

    public static IReadOnlyList<BracketDisplayGroupDto> FromPublic(IReadOnlyList<PublicBracketDisplayGroupDto>? groups)
    {
        if (groups is null || groups.Count == 0)
        {
            return Array.Empty<BracketDisplayGroupDto>();
        }

        return groups.Select(g => new BracketDisplayGroupDto(
            g.Id,
            g.Sequence,
            g.Title,
            g.Subtitle,
            g.Kind,
            g.NodeIds)).ToList();
    }

    private static bool TryCreateLowerGroup(
        IReadOnlyList<BracketTreeNodeDto> nodes,
        out BracketDisplayGroupDto group)
    {
        var losers = nodes
            .Where(n => n.Branch == BracketBranchDto.Losers)
            .OrderBy(n => n.Round)
            .ThenBy(n => n.Slot)
            .ToList();

        if (losers.Count == 0)
        {
            group = default!;
            return false;
        }

        group = CreateGroup(
            "lower",
            0,
            "Lower bracket",
            null,
            BracketDisplayGroupKindDto.Lower,
            losers.Select(n => n.Id).ToList());
        return true;
    }

    private static BracketDisplayGroupDto CreateGroup(
        string id,
        int sequence,
        string title,
        string? subtitle,
        BracketDisplayGroupKindDto kind,
        IReadOnlyList<Guid> nodeIds) =>
        new(id, sequence, title, subtitle, kind, nodeIds);
}
