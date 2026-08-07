using CourtFlow.Application.Features.Stages;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using CourtFlow.Infrastructure.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Tests;

public sealed class DivisionValidationServiceTests
{
    [Fact]
    public async Task BracketOnly_DraftStageWithRankings_DoesNotWarnUnseeded()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var (divisionId, _) = await SeedBracketDivisionAsync(db, teamCount: 4, stageStatus: StageStatus.Draft);

        var result = await new DivisionValidationService(db).EvaluateAsync(divisionId);

        Assert.DoesNotContain(result.Issues, i => i.Message.Contains("not seeded into the bracket"));
    }

    [Fact]
    public async Task BracketOnly_ConfiguredWithEmptySeedSlots_DoesNotWarnUnseeded()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var (divisionId, bracketId) = await SeedBracketDivisionAsync(db, teamCount: 4, stageStatus: StageStatus.Configured);

        for (var pos = 1; pos <= 4; pos++)
        {
            db.BracketSeedSlots.Add(new BracketSeedSlot(
                Guid.NewGuid(), bracketId, pos, "manual", $"Slot {pos}"));
        }
        await db.SaveChangesAsync();

        var result = await new DivisionValidationService(db).EvaluateAsync(divisionId);

        Assert.DoesNotContain(result.Issues, i => i.Message.Contains("not seeded into the bracket"));
    }

    [Fact]
    public async Task BracketOnly_PartialSeed_WarnsUnseededCount()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var (divisionId, bracketId) = await SeedBracketDivisionAsync(db, teamCount: 4, stageStatus: StageStatus.Configured);
        var rankings = await db.DivisionRankings.Where(r => r.DivisionId == divisionId).OrderBy(r => r.Rank).ToListAsync();

        for (var pos = 1; pos <= 4; pos++)
        {
            var slot = new BracketSeedSlot(Guid.NewGuid(), bracketId, pos, "manual", $"Slot {pos}");
            if (pos <= 2)
                slot.Resolve(rankings[pos - 1].TournamentTeamId);
            db.BracketSeedSlots.Add(slot);
        }
        await db.SaveChangesAsync();

        var result = await new DivisionValidationService(db).EvaluateAsync(divisionId);

        Assert.Contains(result.Issues, i => i.Message == "2 ranked team(s) are not seeded into the bracket.");
    }

    [Fact]
    public async Task BracketOnly_FullSeedWithExtraRankedTeam_WarnsOverflow()
    {
        await using var db = ImportFeaturesTests.BuildDb();
        var (divisionId, bracketId) = await SeedBracketDivisionAsync(db, teamCount: 3, stageStatus: StageStatus.Seeded);
        var rankings = await db.DivisionRankings.Where(r => r.DivisionId == divisionId).OrderBy(r => r.Rank).ToListAsync();

        for (var pos = 1; pos <= 2; pos++)
        {
            var slot = new BracketSeedSlot(Guid.NewGuid(), bracketId, pos, "manual", $"Slot {pos}");
            slot.Resolve(rankings[pos - 1].TournamentTeamId);
            db.BracketSeedSlots.Add(slot);
        }
        await db.SaveChangesAsync();

        var result = await new DivisionValidationService(db).EvaluateAsync(divisionId);

        Assert.Contains(result.Issues, i => i.Message == "1 ranked team(s) are not seeded into the bracket.");
    }

    private static async Task<(Guid DivisionId, Guid BracketId)> SeedBracketDivisionAsync(
        TestAppDbContext db,
        int teamCount,
        StageStatus stageStatus)
    {
        var orgId = Guid.NewGuid();
        var tournament = new Tournament(orgId, "T", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3), "America/Los_Angeles");
        var division = new Division(orgId, tournament.Id, "Open", DivisionFormat.BracketOnly, 32);
        db.Tournaments.Add(tournament);
        db.Divisions.Add(division);
        await DefaultDivisionStages.EnsureProvisionedAsync(db, division);
        await db.SaveChangesAsync();

        var bracketStage = await db.Stages.FirstAsync(s => s.DivisionId == division.Id && s.StageType == StageType.Bracket);
        if (stageStatus != StageStatus.Draft)
        {
            bracketStage.TransitionTo(StageStatus.Configured);
            if (stageStatus == StageStatus.Seeded)
                bracketStage.TransitionTo(StageStatus.Seeded);
        }

        var bracket = new Bracket(orgId, tournament.Id, division.Id, BracketType.SingleElimination, 4, false, "Gold");
        db.Brackets.Add(bracket);

        for (var i = 1; i <= teamCount; i++)
        {
            var team = new Team(orgId, division.Id, $"Team {i}", Wave.AM);
            db.Teams.Add(team);
            var tt = new TournamentTeam(orgId, tournament.Id, division.Id, team.Id);
            db.TournamentTeams.Add(tt);
            db.DivisionRankings.Add(new DivisionRanking(orgId, division.Id, tt.Id, i, "test"));
        }

        await db.SaveChangesAsync();
        return (division.Id, bracket.Id);
    }
}
