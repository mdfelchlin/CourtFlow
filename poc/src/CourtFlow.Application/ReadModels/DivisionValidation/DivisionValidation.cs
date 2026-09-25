namespace CourtFlow.Application.ReadModels.DivisionValidation;

public enum DivisionValidationSeverity
{
    Info = 0,
    Warning = 1
}

public sealed record DivisionValidationIssueDto(
    DivisionValidationSeverity Severity,
    string Message,
    string? Location);

public sealed record DivisionValidationResultDto(
    IReadOnlyList<DivisionValidationIssueDto> Issues);

public interface IDivisionValidation
{
    Task<DivisionValidationResultDto> EvaluateAsync(Guid divisionId, CancellationToken ct = default);
}
