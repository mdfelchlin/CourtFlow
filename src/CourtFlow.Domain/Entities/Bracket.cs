using CourtFlow.Domain.Common;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Domain.Entities;

public class Bracket : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid TournamentId { get; private set; }
    public Guid DivisionId { get; private set; }
    public BracketType Type { get; private set; } = BracketType.SingleElimination;
    public bool IfNecessaryFinal { get; private set; }
    public int TeamCapacity { get; private set; }
    public string? DisplayName { get; private set; }

    private readonly List<BracketNode> _nodes = new();
    public IReadOnlyCollection<BracketNode> Nodes => _nodes.AsReadOnly();

    private Bracket() { }

    public Bracket(Guid organizationId, Guid tournamentId, Guid divisionId, BracketType type, int teamCapacity, bool ifNecessaryFinal = false, string? displayName = null)
    {
        OrganizationId = organizationId;
        TournamentId = tournamentId;
        DivisionId = divisionId;
        Type = type;
        TeamCapacity = teamCapacity;
        IfNecessaryFinal = ifNecessaryFinal;
        DisplayName = displayName;
    }

    public BracketNode AddNode(BracketNode node)
    {
        _nodes.Add(node);
        Touch();
        return node;
    }

    public void SetIfNecessaryFinal(bool enabled) { IfNecessaryFinal = enabled; Touch(); }
}
