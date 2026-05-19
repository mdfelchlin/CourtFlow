using CourtFlow.Application.Abstractions;
using CourtFlow.Application.Brackets;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;
using CourtFlow.Infrastructure.Persistence;

namespace CourtFlow.Infrastructure.Brackets;

public class BracketBuilder : IBracketBuilder
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public BracketBuilder(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Bracket> BuildAsync(Guid tournamentId, Guid divisionId, BracketBuildOptions options, CancellationToken ct = default)
    {
        if (options.TeamCapacity < 2) throw new ArgumentException("Bracket needs at least 2 teams");

        var orgId = _tenant.RequireOrganizationId();

        var size = BracketGraphBuilder.NextPowerOfTwo(options.TeamCapacity);
        var bracket = new Bracket(orgId, tournamentId, divisionId, options.Type, size, options.IfNecessaryFinal, options.DisplayName, options.ThirdPlaceMatch);
        _db.Brackets.Add(bracket);

        BracketGraphBuilder.AddNodes(_db, bracket, orgId);

        await _db.SaveChangesAsync(ct);
        return bracket;
    }
}
