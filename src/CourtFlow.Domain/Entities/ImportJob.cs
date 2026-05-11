using CourtFlow.Domain.Common;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Domain.Entities;

public class ImportJob : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid TournamentId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public ImportJobStatus Status { get; private set; } = ImportJobStatus.Pending;
    public int TotalRows { get; private set; }
    public int SuccessRows { get; private set; }
    public int ErrorRows { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private readonly List<ImportRow> _rows = new();
    public IReadOnlyCollection<ImportRow> Rows => _rows.AsReadOnly();

    private ImportJob() { }

    public ImportJob(Guid organizationId, Guid tournamentId, string fileName)
    {
        OrganizationId = organizationId;
        TournamentId = tournamentId;
        FileName = fileName;
    }

    public void MarkCompleted(int successRows, int errorRows)
    {
        Status = ImportJobStatus.Completed;
        SuccessRows = successRows;
        ErrorRows = errorRows;
        CompletedAt = DateTime.UtcNow;
        Touch();
    }

    public void MarkFailed()
    {
        Status = ImportJobStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        Touch();
    }
}
