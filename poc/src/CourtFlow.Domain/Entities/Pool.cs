using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class Pool : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid DivisionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Sequence { get; private set; }
    public int TargetSize { get; private set; } = 4;

    private Pool() { }

    public Pool(Guid organizationId, Guid divisionId, string name, int sequence = 0, int targetSize = 4)
    {
        OrganizationId = organizationId;
        DivisionId = divisionId;
        Sequence = sequence;
        TargetSize = targetSize;
        Rename(name);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Pool name required", nameof(name));
        Name = name.Trim();
        Touch();
    }

    public void SetSequence(int sequence)
    {
        Sequence = sequence;
        Touch();
    }

    public void SetTargetSize(int targetSize)
    {
        if (targetSize < 2 || targetSize > 8)
            throw new ArgumentOutOfRangeException(nameof(targetSize), "Target size must be between 2 and 8.");
        TargetSize = targetSize;
        Touch();
    }
}
