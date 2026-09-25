using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using CourtFlow.Domain.Enums;

namespace CourtFlow.Infrastructure.Brackets;

internal static class BracketGraphBuilder
{
    public static void AddNodes(IAppDbContext db, Bracket bracket, Guid organizationId)
    {
        var size = NextPowerOfTwo(bracket.TeamCapacity);
        var totalRounds = (int)Math.Log2(size);
        var winners = new Dictionary<(int round, int slot), BracketNode>();

        for (var round = 1; round <= totalRounds; round++)
        {
            var slotsInRound = size >> round;
            for (var slot = 0; slot < slotsInRound; slot++)
            {
                winners[(round, slot)] = AddNode(db, bracket, organizationId, BracketBranch.Winners, round, slot);
            }
        }

        var losers = new Dictionary<(int round, int slot), BracketNode>();
        var losersRounds = bracket.Type == BracketType.DoubleElimination
            ? Math.Max(1, totalRounds * 2 - 2)
            : 0;

        if (bracket.Type == BracketType.DoubleElimination)
        {
            for (var round = 1; round <= losersRounds; round++)
            {
                var slotsInRound = LoserSlots(size, round);
                for (var slot = 0; slot < slotsInRound; slot++)
                {
                    losers[(round, slot)] = AddNode(db, bracket, organizationId, BracketBranch.Losers, round, slot);
                }
            }
        }

        BracketNode? grandFinal = null;
        if (bracket.Type == BracketType.DoubleElimination)
        {
            grandFinal = AddNode(db, bracket, organizationId, BracketBranch.GrandFinal, 1, 0);
            if (bracket.IfNecessaryFinal)
            {
                var ifNecessary = AddNode(db, bracket, organizationId, BracketBranch.GrandFinal, 2, 0);
                grandFinal.WireAdvancement(ifNecessary.Id, 1, ifNecessary.Id, 2);
            }
        }

        BracketNode? thirdPlace = null;
        if (bracket.ThirdPlaceMatch && totalRounds > 1)
        {
            thirdPlace = AddNode(db, bracket, organizationId, BracketBranch.ThirdPlace, 1, 0);
        }

        for (var round = 1; round <= totalRounds; round++)
        {
            var slotsInRound = size >> round;
            for (var slot = 0; slot < slotsInRound; slot++)
            {
                Guid? winsToNodeId = null;
                int? winsToSlot = null;
                if (round < totalRounds)
                {
                    winsToNodeId = winners[(round + 1, slot / 2)].Id;
                    winsToSlot = slot % 2 == 0 ? 1 : 2;
                }
                else if (grandFinal is not null)
                {
                    winsToNodeId = grandFinal.Id;
                    winsToSlot = 1;
                }

                var losesTo = bracket.Type == BracketType.DoubleElimination
                    ? thirdPlace is not null && round == totalRounds - 1
                        ? (thirdPlace.Id, (int?)(slot + 1))
                        : WinnerLoserDestination(losers, totalRounds, losersRounds, round, slot)
                    : thirdPlace is not null && round == totalRounds - 1
                        ? (thirdPlace.Id, (int?)(slot + 1))
                        : (null, null);

                winners[(round, slot)].WireAdvancement(winsToNodeId, winsToSlot, losesTo.Item1, losesTo.Item2);
            }
        }

        if (grandFinal is not null)
        {
            WireLosers(losers, losersRounds, grandFinal.Id);
        }
    }

    private static BracketNode AddNode(
        IAppDbContext db,
        Bracket bracket,
        Guid organizationId,
        BracketBranch branch,
        int round,
        int slot)
    {
        var node = new BracketNode(organizationId, bracket.Id, branch, round, slot);
        bracket.AddNode(node);
        db.BracketNodes.Add(node);
        return node;
    }

    private static (Guid?, int?) WinnerLoserDestination(
        Dictionary<(int round, int slot), BracketNode> losers,
        int totalRounds,
        int losersRounds,
        int winnersRound,
        int winnersSlot)
    {
        if (losersRounds == 0) return (null, null);

        if (winnersRound == 1)
        {
            var targetSlot = Math.Min(winnersSlot / 2, MaxSlot(losers, 1));
            return (losers[(1, targetSlot)].Id, winnersSlot % 2 == 0 ? 1 : 2);
        }

        var targetRound = winnersRound == totalRounds
            ? losersRounds
            : Math.Min(losersRounds, winnersRound * 2 - 2);
        return (losers[(targetRound, Math.Min(winnersSlot, MaxSlot(losers, targetRound)))].Id, 2);
    }

    private static int MaxSlot(Dictionary<(int round, int slot), BracketNode> nodes, int round) =>
        nodes.Keys.Where(k => k.round == round).Select(k => k.slot).DefaultIfEmpty(0).Max();

    private static void WireLosers(Dictionary<(int round, int slot), BracketNode> losers, int losersRounds, Guid grandFinalId)
    {
        for (var round = 1; round <= losersRounds; round++)
        {
            var roundSlots = losers.Keys.Where(k => k.round == round).Select(k => k.slot).ToList();
            foreach (var slot in roundSlots)
            {
                var node = losers[(round, slot)];
                if (round == losersRounds)
                {
                    node.WireAdvancement(grandFinalId, 2, null, null);
                    continue;
                }

                var nextSlot = round % 2 == 1 ? slot : slot / 2;
                var nextSlotNumber = round % 2 == 1
                    ? 1
                    : slot % 2 == 0 ? 1 : 2;
                node.WireAdvancement(losers[(round + 1, nextSlot)].Id, nextSlotNumber, null, null);
            }
        }
    }

    private static int LoserSlots(int bracketSize, int round) =>
        Math.Max(1, bracketSize >> ((round + 1) / 2 + 1));

    public static int NextPowerOfTwo(int n)
    {
        var p = 1;
        while (p < n) p <<= 1;
        return p;
    }
}
