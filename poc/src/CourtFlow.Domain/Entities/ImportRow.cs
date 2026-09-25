using CourtFlow.Domain.Common;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Domain.Entities;

public class ImportRow : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid ImportJobId { get; private set; }
    public int RowNumber { get; private set; }
    public string? RawValues { get; private set; }
    public ImportRowStatus Status { get; private set; } = ImportRowStatus.Pending;
    public string? ValidationErrors { get; private set; }
    public Guid? MatchedTeamId { get; private set; }
    public Guid? MatchedClubId { get; private set; }

    private ImportRow() { }

    public ImportRow(Guid organizationId, Guid importJobId, int rowNumber, string? rawValues)
    {
        OrganizationId = organizationId;
        ImportJobId = importJobId;
        RowNumber = rowNumber;
        RawValues = rawValues;
    }

    public void MarkMatched(Guid teamId, Guid? clubId)
    {
        Status = ImportRowStatus.Matched;
        MatchedTeamId = teamId;
        MatchedClubId = clubId;
        Touch();
    }

    public void MarkError(string errors)
    {
        Status = ImportRowStatus.Error;
        ValidationErrors = errors;
        Touch();
    }
}
