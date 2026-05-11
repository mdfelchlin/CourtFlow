using CourtFlow.Application.Reflow;

namespace CourtFlow.Infrastructure.Reflow;

/// <inheritdoc />
public sealed class NoOpReflowPreviewEngine : IReflowPreviewEngine
{
    public Task<ReflowPreviewDelta> PreviewAsync(Guid tournamentId, CancellationToken ct = default)
    {
        _ = tournamentId;
        return Task.FromResult(new ReflowPreviewDelta(Array.Empty<string>()));
    }
}
