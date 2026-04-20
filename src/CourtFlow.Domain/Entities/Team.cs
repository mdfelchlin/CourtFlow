using CourtFlow.Domain.Common;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Domain.Entities;

public class Team : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid DivisionId { get; private set; }
    public Guid? PoolId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Club { get; private set; }
    public Wave Wave { get; private set; } = Wave.AM;
    public int? SeedHint { get; private set; }

    private Team() { }

    public Team(Guid organizationId, Guid divisionId, string name, Wave wave, Guid? poolId = null, string? club = null, int? seedHint = null)
    {
        OrganizationId = organizationId;
        DivisionId = divisionId;
        PoolId = poolId;
        Rename(name);
        Wave = wave;
        Club = club;
        SeedHint = seedHint;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Team name required", nameof(name));
        Name = name.Trim();
        Touch();
    }

    public void AssignPool(Guid? poolId) { PoolId = poolId; Touch(); }
    public void SetWave(Wave wave) { Wave = wave; Touch(); }
    public void SetSeedHint(int? seedHint) { SeedHint = seedHint; Touch(); }
    public void SetClub(string? club) { Club = club; Touch(); }
}
