using MediatR;

namespace CourtFlow.Application.StageMaterialization.Commands;

public sealed record SnakeIntoPoolsCommand(Guid DivisionId) : IRequest;

public sealed class SnakeIntoPoolsHandler(IStageMaterialization materialization)
    : IRequestHandler<SnakeIntoPoolsCommand>
{
    public Task Handle(SnakeIntoPoolsCommand request, CancellationToken cancellationToken) =>
        materialization.SnakeIntoPoolsAsync(request.DivisionId, cancellationToken);
}

public sealed record GeneratePoolMatchesCommand(Guid DivisionId) : IRequest;

public sealed class GeneratePoolMatchesHandler(IStageMaterialization materialization)
    : IRequestHandler<GeneratePoolMatchesCommand>
{
    public Task Handle(GeneratePoolMatchesCommand request, CancellationToken cancellationToken) =>
        materialization.GeneratePoolMatchesAsync(request.DivisionId, cancellationToken);
}

public sealed record GenerateBracketsCommand(Guid DivisionId) : IRequest;

public sealed class GenerateBracketsHandler(IStageMaterialization materialization)
    : IRequestHandler<GenerateBracketsCommand>
{
    public Task Handle(GenerateBracketsCommand request, CancellationToken cancellationToken) =>
        materialization.GenerateBracketsAsync(request.DivisionId, cancellationToken);
}

public sealed record ResolveBracketSlotsCommand(Guid DivisionId) : IRequest;

public sealed class ResolveBracketSlotsHandler(IStageMaterialization materialization)
    : IRequestHandler<ResolveBracketSlotsCommand>
{
    public Task Handle(ResolveBracketSlotsCommand request, CancellationToken cancellationToken) =>
        materialization.ResolveBracketSlotsAsync(request.DivisionId, cancellationToken);
}

public sealed record SeedBracketFromRankingsCommand(Guid DivisionId) : IRequest;

public sealed class SeedBracketFromRankingsHandler(IStageMaterialization materialization)
    : IRequestHandler<SeedBracketFromRankingsCommand>
{
    public Task Handle(SeedBracketFromRankingsCommand request, CancellationToken cancellationToken) =>
        materialization.SeedBracketFromRankingsAsync(request.DivisionId, cancellationToken);
}
