using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class BracketSeedSlot : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid BracketId { get; private set; }
    public int SeedPosition { get; private set; }
    public string? Label { get; private set; }
    public string ExpectedSourceType { get; private set; } = "advancement_rule";
    public Guid? AdvancementRuleId { get; private set; }
    public Guid? ManualTournamentTeamId { get; private set; }
    public Guid? ResolvedTournamentTeamId { get; private set; }

    private BracketSeedSlot() { }

    public BracketSeedSlot(Guid organizationId, Guid bracketId, int seedPosition, string expectedSourceType, string? label = null)
    {
        OrganizationId = organizationId;
        BracketId = bracketId;
        SeedPosition = seedPosition;
        ExpectedSourceType = expectedSourceType;
        Label = label;
    }

    public void SetManualOverride(Guid? tournamentTeamId)
    {
        ManualTournamentTeamId = tournamentTeamId;
        Touch();
    }

    public void Resolve(Guid? tournamentTeamId)
    {
        ResolvedTournamentTeamId = tournamentTeamId;
        Touch();
    }

    public void LinkAdvancementRule(Guid? ruleId)
    {
        AdvancementRuleId = ruleId;
        Touch();
    }
}
