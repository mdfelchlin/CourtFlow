namespace CourtFlow.Application.Reflow;

/// <summary>Preview-only reflow (ADR-0004 / ADR-0007 seam). Implementations may start as no-op.</summary>
public interface IReflowPreviewEngine
{
    Task<ReflowPreviewDelta> PreviewAsync(Guid tournamentId, CancellationToken ct = default);
}

public sealed record ReflowPreviewDelta(IReadOnlyList<string> Notes);
