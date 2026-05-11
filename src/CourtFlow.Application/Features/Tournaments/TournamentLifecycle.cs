using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Tournaments;

public sealed record PublishTournamentCommand(Guid Id) : IRequest<TournamentDto?>;
public sealed record UnpublishTournamentCommand(Guid Id) : IRequest<TournamentDto?>;
public sealed record ArchiveTournamentCommand(Guid Id) : IRequest<TournamentDto?>;
public sealed record RestoreArchivedTournamentCommand(Guid Id) : IRequest<TournamentDto?>;

public sealed class PublishTournamentValidator : AbstractValidator<PublishTournamentCommand>
{
    public PublishTournamentValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class UnpublishTournamentValidator : AbstractValidator<UnpublishTournamentCommand>
{
    public UnpublishTournamentValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class ArchiveTournamentValidator : AbstractValidator<ArchiveTournamentCommand>
{
    public ArchiveTournamentValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class RestoreArchivedTournamentValidator : AbstractValidator<RestoreArchivedTournamentCommand>
{
    public RestoreArchivedTournamentValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class PublishTournamentHandler : IRequestHandler<PublishTournamentCommand, TournamentDto?>
{
    private readonly IAppDbContext _db;

    public PublishTournamentHandler(IAppDbContext db) => _db = db;

    public async Task<TournamentDto?> Handle(PublishTournamentCommand request, CancellationToken ct)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (t is null) return null;
        t.Publish();
        await _db.SaveChangesAsync(ct);
        return t.ToDto();
    }
}

public sealed class UnpublishTournamentHandler : IRequestHandler<UnpublishTournamentCommand, TournamentDto?>
{
    private readonly IAppDbContext _db;

    public UnpublishTournamentHandler(IAppDbContext db) => _db = db;

    public async Task<TournamentDto?> Handle(UnpublishTournamentCommand request, CancellationToken ct)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (t is null) return null;
        t.Unpublish();
        await _db.SaveChangesAsync(ct);
        return t.ToDto();
    }
}

public sealed class ArchiveTournamentHandler : IRequestHandler<ArchiveTournamentCommand, TournamentDto?>
{
    private readonly IAppDbContext _db;

    public ArchiveTournamentHandler(IAppDbContext db) => _db = db;

    public async Task<TournamentDto?> Handle(ArchiveTournamentCommand request, CancellationToken ct)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (t is null) return null;
        t.Archive();
        await _db.SaveChangesAsync(ct);
        return t.ToDto();
    }
}

public sealed class RestoreArchivedTournamentHandler : IRequestHandler<RestoreArchivedTournamentCommand, TournamentDto?>
{
    private readonly IAppDbContext _db;

    public RestoreArchivedTournamentHandler(IAppDbContext db) => _db = db;

    public async Task<TournamentDto?> Handle(RestoreArchivedTournamentCommand request, CancellationToken ct)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (t is null) return null;
        t.RestoreFromArchive();
        await _db.SaveChangesAsync(ct);
        return t.ToDto();
    }
}
