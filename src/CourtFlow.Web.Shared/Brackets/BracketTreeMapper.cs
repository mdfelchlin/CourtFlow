using CourtFlow.ApiClient;

namespace CourtFlow.Web.Shared.Brackets;

public static class BracketTreeMapper
{
    public static IReadOnlyList<BracketTreeNodeDto> FromPublic(PublicBracketDto bracket) =>
        bracket.Nodes.Select(n => FromPublicNode(n, bracket.Id)).ToList();

    public static IReadOnlyList<BracketDisplayGroupDto> GroupsFromPublic(PublicBracketDto bracket) =>
        BracketDisplayGrouper.FromPublic(bracket.Groups).Count > 0
            ? BracketDisplayGrouper.FromPublic(bracket.Groups)
            : BracketDisplayGrouper.BuildGroups(FromPublic(bracket));

    public static BracketTreeNodeDto FromPublicNode(PublicBracketNodeDto n, Guid bracketId) =>
        new(
            n.Id,
            bracketId,
            n.Branch,
            n.Round,
            n.Slot,
            n.MatchId,
            n.HomeTeamId,
            n.AwayTeamId,
            n.HomeSeed,
            n.AwaySeed,
            n.WinsToNodeId,
            n.LosesToNodeId,
            n.HomeLabel,
            n.AwayLabel,
            n.HomeIsBye,
            n.AwayIsBye,
            n.IsPinnedHome,
            n.IsPinnedAway,
            n.MatchState,
            n.Sets.Select(s => new BracketTreeSetScoreDto(s.SetNumber, s.HomeScore, s.AwayScore)).ToList(),
            n.HomeSetsSummary,
            n.AwaySetsSummary,
            n.WinnerTeamId,
            n.WinnerSide,
            n.CourtName,
            n.ScheduledStartUtc,
            n.MatchLabel);
}
