using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Brackets;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Brackets;

public sealed record BracketDto(Guid Id, Guid TournamentId, Guid DivisionId, BracketType Type, bool IfNecessaryFinal, int TeamCapacity, string? DisplayName);
public sealed record BracketNodeDto(Guid Id, Guid BracketId, BracketBranch Branch, int Round, int Slot, Guid? MatchId, Guid? HomeTeamId, Guid? AwayTeamId, int? HomeSeed, int? AwaySeed, Guid? WinsToNodeId, Guid? LosesToNodeId);
public sealed record BracketWithNodesDto(BracketDto Bracket, IReadOnlyList<BracketNodeDto> Nodes);

internal static class BracketMapping
{
    public static BracketDto ToDto(this Bracket b) => new(b.Id, b.TournamentId, b.DivisionId, b.Type, b.IfNecessaryFinal, b.TeamCapacity, b.DisplayName);
    public static BracketNodeDto ToDto(this BracketNode n) => new(
        n.Id, n.BracketId, n.Branch, n.Round, n.Slot, n.MatchId,
        n.HomeTeamId, n.AwayTeamId, n.HomeSeed, n.AwaySeed,
        n.WinsToNodeId, n.LosesToNodeId);
}

public sealed record ListBracketsQuery(Guid? DivisionId) : IRequest<IReadOnlyList<BracketDto>>;

public sealed class ListBracketsHandler : IRequestHandler<ListBracketsQuery, IReadOnlyList<BracketDto>>
{
    private readonly IAppDbContext _db;
    public ListBracketsHandler(IAppDbContext db) => _db = db;
    public async Task<IReadOnlyList<BracketDto>> Handle(ListBracketsQuery request, CancellationToken ct)
    {
        var q = _db.Brackets.AsQueryable();
        if (request.DivisionId is { } d) q = q.Where(b => b.DivisionId == d);
        var items = await q.ToListAsync(ct);
        return items.Select(BracketMapping.ToDto).ToList();
    }
}

public sealed record GetBracketQuery(Guid Id) : IRequest<BracketWithNodesDto?>;

public sealed class GetBracketHandler : IRequestHandler<GetBracketQuery, BracketWithNodesDto?>
{
    private readonly IAppDbContext _db;
    public GetBracketHandler(IAppDbContext db) => _db = db;
    public async Task<BracketWithNodesDto?> Handle(GetBracketQuery request, CancellationToken ct)
    {
        var b = await _db.Brackets.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (b is null) return null;
        var nodes = await _db.BracketNodes.Where(n => n.BracketId == b.Id).ToListAsync(ct);
        return new BracketWithNodesDto(b.ToDto(), nodes.Select(BracketMapping.ToDto).ToList());
    }
}

public sealed record GenerateBracketCommand(Guid TournamentId, Guid DivisionId, BracketType Type, int TeamCapacity, bool IfNecessaryFinal, string? DisplayName = null) : IRequest<BracketDto>;

public sealed class GenerateBracketHandler : IRequestHandler<GenerateBracketCommand, BracketDto>
{
    private readonly IBracketBuilder _builder;
    public GenerateBracketHandler(IBracketBuilder builder) => _builder = builder;
    public async Task<BracketDto> Handle(GenerateBracketCommand request, CancellationToken ct)
    {
        var bracket = await _builder.BuildAsync(request.TournamentId, request.DivisionId,
            new BracketBuildOptions(request.Type, request.TeamCapacity, request.IfNecessaryFinal, request.DisplayName), ct);
        return bracket.ToDto();
    }
}
