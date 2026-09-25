using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class WaveEntity : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid TournamentId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public string? DefaultStartTime { get; private set; }
    public string? DefaultEndTime { get; private set; }
    public string? Notes { get; private set; }

    private WaveEntity() { }

    public WaveEntity(Guid organizationId, Guid tournamentId, string code, int displayOrder)
    {
        OrganizationId = organizationId;
        TournamentId = tournamentId;
        Code = code;
        DisplayOrder = displayOrder;
    }

    public void SetTimeWindow(string? startTime, string? endTime)
    {
        DefaultStartTime = startTime;
        DefaultEndTime = endTime;
        Touch();
    }

    public void SetNotes(string? notes)
    {
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Touch();
    }
}
