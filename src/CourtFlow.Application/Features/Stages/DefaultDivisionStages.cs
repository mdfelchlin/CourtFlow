using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Application.Features.Stages;

public static class DefaultDivisionStages
{
    /// <summary>Idempotent pool (stage 1) + bracket (stage 2) rows for every new division.</summary>
    public static async Task EnsureProvisionedAsync(IAppDbContext db, Division division, CancellationToken ct = default)
    {
        if (await db.Stages.AsNoTracking().AnyAsync(s => s.DivisionId == division.Id, ct))
            return;

        db.Stages.AddRange(
            new Stage(division.OrganizationId, division.Id, 1, StageType.Pool, "Pool play"),
            new Stage(division.OrganizationId, division.Id, 2, StageType.Bracket, "Bracket"));
    }
}
