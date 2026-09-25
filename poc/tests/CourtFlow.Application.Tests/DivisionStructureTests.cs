using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Features.Divisions;
using CourtFlow.Application.Features.Stages;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using CourtFlow.Infrastructure.StageMaterialization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Tests;

public sealed class DivisionStructureTests
{
    [Fact]
    public async Task SavePoolStructure_ThenSnakeIntoPools_DoesNotThrowConcurrency()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T1", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.PoolPlusBracket, 32);
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        await DefaultDivisionStages.EnsureProvisionedAsync(db, division);
        await db.SaveChangesAsync();

        for (var i = 1; i <= 6; i++)
        {
            var team = new Team(orgId, division.Id, $"Team {i}", Wave.AM);
            db.Teams.Add(team);
            var tt = new TournamentTeam(orgId, tournament.Id, division.Id, team.Id);
            db.TournamentTeams.Add(tt);
            db.DivisionRankings.Add(new DivisionRanking(orgId, division.Id, tt.Id, i, "test"));
        }
        await db.SaveChangesAsync();

        var tenant = new FakeTenant(orgId);
        var mediator = new TestMediator(db, tenant);
        var saveHandler = new SaveDivisionPoolStructureHandler(db, tenant, mediator);

        var poolDtos = new List<DivisionPoolStructureDto>
        {
            new(Guid.Empty, "Pool A", 0, 4, Array.Empty<PoolCourtAllocationDto>()),
            new(Guid.Empty, "Pool B", 1, 4, Array.Empty<PoolCourtAllocationDto>())
        };

        var wizard = await saveHandler.Handle(new SaveDivisionPoolStructureCommand(division.Id, poolDtos), CancellationToken.None);
        Assert.NotNull(wizard);
        Assert.Equal(2, wizard.Pools.Count);

        poolDtos = wizard.Pools
            .Select(p => new DivisionPoolStructureDto(p.Id, p.Name, p.Sequence, p.TargetSize, p.CourtAllocations))
            .ToList();

        wizard = await saveHandler.Handle(new SaveDivisionPoolStructureCommand(division.Id, poolDtos), CancellationToken.None);
        Assert.NotNull(wizard);

        var materializer = new StageMaterializationService(db, new FakeStandingsCalculator());
        await materializer.SnakeIntoPoolsAsync(division.Id);

        wizard = await saveHandler.Handle(new SaveDivisionPoolStructureCommand(division.Id, poolDtos), CancellationToken.None);
        Assert.NotNull(wizard);

        var entries = await db.PoolEntries.CountAsync();
        Assert.Equal(6, entries);

        var poolCount = await db.Pools.CountAsync(p => p.DivisionId == division.Id);
        Assert.Equal(2, poolCount);
    }

    [Fact]
    public async Task SavePoolStructure_AfterSnake_WithFewerPoolsInPayload_DoesNotDeleteMaterializedPools()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T", new DateOnly(2026, 5, 12), new DateOnly(2026, 5, 14), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Large", DivisionFormat.PoolPlusBracket, 32);
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        await DefaultDivisionStages.EnsureProvisionedAsync(db, division);
        await db.SaveChangesAsync();

        for (var i = 1; i <= 11; i++)
        {
            var team = new Team(orgId, division.Id, $"Team {i:D2}", Wave.AM);
            db.Teams.Add(team);
            var tt = new TournamentTeam(orgId, tournament.Id, division.Id, team.Id);
            db.TournamentTeams.Add(tt);
            db.DivisionRankings.Add(new DivisionRanking(orgId, division.Id, tt.Id, i, "test"));
        }
        await db.SaveChangesAsync();

        var tenant = new FakeTenant(orgId);
        var mediator = new TestMediator(db, tenant);
        var saveHandler = new SaveDivisionPoolStructureHandler(db, tenant, mediator);

        var onePool = new List<DivisionPoolStructureDto>
        {
            new(Guid.Empty, "Pool A", 0, 4, Array.Empty<PoolCourtAllocationDto>())
        };

        var wizard = await saveHandler.Handle(new SaveDivisionPoolStructureCommand(division.Id, onePool), CancellationToken.None);
        Assert.NotNull(wizard);
        Assert.Single(wizard.Pools);

        var materializer = new StageMaterializationService(db, new FakeStandingsCalculator());
        await materializer.SnakeIntoPoolsAsync(division.Id);

        var poolCountAfterSnake = await db.Pools.CountAsync(p => p.DivisionId == division.Id);
        Assert.Equal(3, poolCountAfterSnake);

        var saveAfterSnake = await saveHandler.Handle(
            new SaveDivisionPoolStructureCommand(division.Id, onePool), CancellationToken.None);

        Assert.NotNull(saveAfterSnake);
        Assert.Equal(3, await db.Pools.CountAsync(p => p.DivisionId == division.Id));
        Assert.Equal(11, await db.PoolEntries.CountAsync());
    }

    [Fact]
    public async Task EnsureMissingStagesForFormat_BracketAtOne_AddsPoolAtTwoForPoolPlusBracket()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.BracketOnly, 16);
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        await DefaultDivisionStages.EnsureProvisionedAsync(db, division);
        await db.SaveChangesAsync();

        division.SetFormat(DivisionFormat.PoolPlusBracket);
        await DefaultDivisionStages.EnsureMissingStagesForFormatAsync(db, division);
        await db.SaveChangesAsync();

        var stages = await db.Stages.Where(s => s.DivisionId == division.Id).OrderBy(s => s.StageNumber).ToListAsync();
        Assert.Equal(2, stages.Count);
        Assert.Equal(StageType.Bracket, stages[0].StageType);
        Assert.Equal(1, stages[0].StageNumber);
        Assert.Equal(StageType.Pool, stages[1].StageType);
        Assert.Equal(2, stages[1].StageNumber);
    }

    private sealed class FakeTenant(Guid orgId) : ITenantContext
    {
        public Guid? OrganizationId => orgId;
        public string? UserId => "test";
        public bool IsAuthenticated => true;
        public Guid RequireOrganizationId() => orgId;
        public string RequireUserId() => "test";
    }

    private sealed class TestMediator(IAppDbContext db, ITenantContext tenant) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetDivisionWizardStateQuery q)
                return (Task<TResponse>)(object)new GetDivisionWizardStateHandler(db, tenant).Handle(q, cancellationToken);
            throw new NotSupportedException(request.GetType().Name);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
            => throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
            => Task.CompletedTask;
    }
}
