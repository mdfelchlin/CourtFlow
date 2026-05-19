using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Application.Features.Brackets;

public enum BracketDisplayGroupKind
{
    Main = 1,
    Lower = 2,
    SingleMatch = 3,
    GrandFinal = 4,
    ThirdPlace = 5
}

public sealed record BracketDisplayGroupDto(
    string Id,
    int Sequence,
    string Title,
    string? Subtitle,
    BracketDisplayGroupKind Kind,
    IReadOnlyList<Guid> NodeIds);

public static class BracketDisplayGrouper
{
    public static IReadOnlyList<BracketDisplayGroupDto> BuildGroups(IReadOnlyList<BracketNode> nodes)
    {
        if (nodes.Count == 0)
        {
            return Array.Empty<BracketDisplayGroupDto>();
        }

        var groups = new List<BracketDisplayGroupDto>();
        var sequence = 0;

        var winners = nodes
            .Where(n => n.Branch == BracketBranch.Winners)
            .OrderBy(n => n.Round)
            .ThenBy(n => n.Slot)
            .ToList();

        if (winners.Count > 0)
        {
            groups.Add(CreateGroup(
                "main",
                sequence++,
                "Main bracket",
                subtitle: null,
                BracketDisplayGroupKind.Main,
                winners.Select(n => n.Id).ToList()));
        }
        else if (!nodes.Any(n => n.Branch == BracketBranch.Losers))
        {
            groups.Add(CreateGroup(
                "main",
                sequence++,
                "Main bracket",
                subtitle: null,
                BracketDisplayGroupKind.Main,
                nodes.Select(n => n.Id).ToList()));
            return groups;
        }

        if (TryCreateLowerGroup(nodes, out var lowerGroup))
        {
            groups.Add(lowerGroup with { Sequence = sequence++ });
        }

        var grandFinal = nodes
            .Where(n => n.Branch == BracketBranch.GrandFinal)
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
                subtitle: null,
                grandFinal.Count == 1 ? BracketDisplayGroupKind.SingleMatch : BracketDisplayGroupKind.GrandFinal,
                grandFinal));
        }

        var thirdPlace = nodes
            .Where(n => n.Branch == BracketBranch.ThirdPlace)
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
                subtitle: null,
                BracketDisplayGroupKind.ThirdPlace,
                thirdPlace));
        }

        return groups;
    }

    private static bool TryCreateLowerGroup(
        IReadOnlyList<BracketNode> nodes,
        out BracketDisplayGroupDto group)
    {
        var losers = nodes
            .Where(n => n.Branch == BracketBranch.Losers)
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
            sequence: 0,
            "Lower bracket",
            subtitle: null,
            BracketDisplayGroupKind.Lower,
            losers.Select(n => n.Id).ToList());
        return true;
    }

    private static BracketDisplayGroupDto CreateGroup(
        string id,
        int sequence,
        string title,
        string? subtitle,
        BracketDisplayGroupKind kind,
        IReadOnlyList<Guid> nodeIds) =>
        new(id, sequence, title, subtitle, kind, nodeIds);
}
