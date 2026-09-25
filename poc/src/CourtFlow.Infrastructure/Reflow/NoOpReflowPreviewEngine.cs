using CourtFlow.Application.Reflow;

namespace CourtFlow.Infrastructure.Reflow;

/// <inheritdoc />
public sealed class NoOpReflowPreviewEngine : IReflowPreviewEngine
{
    public Task<ReflowPreviewDelta> PreviewAsync(Guid tournamentId, CancellationToken ct = default)
    {
        _ = tournamentId;
        return Task.FromResult(new ReflowPreviewDelta(
            new[] { "Reflow preview not configured." },
            Array.Empty<ReflowAssignmentMove>(),
            null));
    }

    public Task<ReflowApplyResult> ApplyAsync(Guid tournamentId, string applyToken, string? createdByUserId, CancellationToken ct = default)
    {
        _ = (tournamentId, applyToken, createdByUserId);
        throw new InvalidOperationException("Reflow apply is not configured.");
    }
}
