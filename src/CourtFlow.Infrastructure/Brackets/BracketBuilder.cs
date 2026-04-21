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

        var size = NextPowerOfTwo(options.TeamCapacity);
        var bracket = new Bracket(orgId, tournamentId, divisionId, options.Type, size, options.IfNecessaryFinal, options.DisplayName);
        _db.Brackets.Add(bracket);

        var winners = new Dictionary<(int round, int slot), BracketNode>();
        var totalRounds = (int)Math.Log2(size);

        for (var round = 1; round <= totalRounds; round++)
        {
            var slotsInRound = size >> round;
            for (var slot = 0; slot < slotsInRound; slot++)
            {
                var node = new BracketNode(orgId, bracket.Id, BracketBranch.Winners, round, slot);
                bracket.AddNode(node);
                _db.BracketNodes.Add(node);
                winners[(round, slot)] = node;
            }
        }

        for (var round = 1; round < totalRounds; round++)
        {
            var slotsInRound = size >> round;
            for (var slot = 0; slot < slotsInRound; slot++)
            {
                var parentSlot = slot / 2;
                var parentSlotNumber = (slot % 2 == 0) ? 1 : 2;
                winners[(round, slot)].WireAdvancement(winners[(round + 1, parentSlot)].Id, parentSlotNumber, null, null);
            }
        }

        if (options.Type == BracketType.DoubleElimination)
        {
            var losers = new Dictionary<(int round, int slot), BracketNode>();
            var losersRounds = totalRounds * 2 - 1;
            for (var round = 1; round <= losersRounds; round++)
            {
                var slotsInRound = Math.Max(1, size >> ((round + 1) / 2 + 1));
                for (var slot = 0; slot < slotsInRound; slot++)
                {
                    var node = new BracketNode(orgId, bracket.Id, BracketBranch.Losers, round, slot);
                    bracket.AddNode(node);
                    _db.BracketNodes.Add(node);
                    losers[(round, slot)] = node;
                }
            }

            var grandFinal = new BracketNode(orgId, bracket.Id, BracketBranch.GrandFinal, 1, 0);
            bracket.AddNode(grandFinal);
            _db.BracketNodes.Add(grandFinal);

            if (options.IfNecessaryFinal)
            {
                var ifNecessary = new BracketNode(orgId, bracket.Id, BracketBranch.GrandFinal, 2, 0);
                bracket.AddNode(ifNecessary);
                _db.BracketNodes.Add(ifNecessary);
                grandFinal.WireAdvancement(ifNecessary.Id, 1, ifNecessary.Id, 2);
            }
        }

        await _db.SaveChangesAsync(ct);
        return bracket;
    }

    private static int NextPowerOfTwo(int n)
    {
        var p = 1;
        while (p < n) p <<= 1;
        return p;
    }
}
