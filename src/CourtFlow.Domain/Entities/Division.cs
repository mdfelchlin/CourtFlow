using CourtFlow.Domain.Common;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Domain.Entities;

public class Division : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid TournamentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DivisionFormat Format { get; private set; } = DivisionFormat.PoolPlusBracket;
    public int MaxTeams { get; private set; } = 32;
    public Guid? WaveId { get; private set; }
    public DivisionTiebreakerPreset TiebreakerPreset { get; private set; } = DivisionTiebreakerPreset.WinsH2HSetsPoints;

    private readonly List<Pool> _pools = new();
    public IReadOnlyCollection<Pool> Pools => _pools.AsReadOnly();

    private readonly List<Team> _teams = new();
    public IReadOnlyCollection<Team> Teams => _teams.AsReadOnly();

    private Division() { }

    public Division(Guid organizationId, Guid tournamentId, string name, DivisionFormat format, int maxTeams)
    {
        OrganizationId = organizationId;
        TournamentId = tournamentId;
        Rename(name);
        Format = format;
        SetMaxTeams(maxTeams);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Division name required", nameof(name));
        Name = name.Trim();
        Touch();
    }

    public void SetFormat(DivisionFormat format) { Format = format; Touch(); }

    public void SetMaxTeams(int maxTeams)
    {
        if (maxTeams <= 0) throw new ArgumentException("Max teams must be > 0", nameof(maxTeams));
        MaxTeams = maxTeams;
        Touch();
    }

    public void SetWave(Guid? waveId)
    {
        WaveId = waveId;
        Touch();
    }

    public void SetTiebreakerPreset(DivisionTiebreakerPreset preset)
    {
        TiebreakerPreset = preset;
        Touch();
    }
}
