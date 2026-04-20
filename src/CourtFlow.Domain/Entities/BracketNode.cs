using CourtFlow.Domain.Common;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Domain.Entities;

public class BracketNode : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid BracketId { get; private set; }
    public BracketBranch Branch { get; private set; } = BracketBranch.Winners;
    public int Round { get; private set; }
    public int Slot { get; private set; }
    public Guid? MatchId { get; private set; }

    public Guid? WinsToNodeId { get; private set; }
    public int? WinsToSlot { get; private set; }
    public Guid? LosesToNodeId { get; private set; }
    public int? LosesToSlot { get; private set; }

    public Guid? HomeTeamId { get; private set; }
    public Guid? AwayTeamId { get; private set; }
    public int? HomeSeed { get; private set; }
    public int? AwaySeed { get; private set; }

    private BracketNode() { }

    public BracketNode(Guid organizationId, Guid bracketId, BracketBranch branch, int round, int slot)
    {
        OrganizationId = organizationId;
        BracketId = bracketId;
        Branch = branch;
        Round = round;
        Slot = slot;
    }

    public void LinkMatch(Guid matchId) { MatchId = matchId; Touch(); }
    public void WireAdvancement(Guid? winsToNodeId, int? winsToSlot, Guid? losesToNodeId, int? losesToSlot)
    {
        WinsToNodeId = winsToNodeId;
        WinsToSlot = winsToSlot;
        LosesToNodeId = losesToNodeId;
        LosesToSlot = losesToSlot;
        Touch();
    }
    public void AssignTeam(int slotNumber, Guid? teamId, int? seed = null)
    {
        if (slotNumber == 1) { HomeTeamId = teamId; HomeSeed = seed; }
        else if (slotNumber == 2) { AwayTeamId = teamId; AwaySeed = seed; }
        else throw new ArgumentOutOfRangeException(nameof(slotNumber), "Slot must be 1 (home) or 2 (away)");
        Touch();
    }
}
