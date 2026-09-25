using CourtFlow.ApiClient;

namespace CourtFlow.Web.Shared.Brackets;

public sealed record BracketLayoutNodeInput(
    Guid Id,
    BracketBranchDto Branch,
    int Round,
    int Slot,
    Guid? WinsToNodeId,
    Guid? LosesToNodeId);

public sealed record BracketLayoutOptions(
    double CardWidth = 196,
    double CardHeight = 56,
    double ConnectorGap = 48,
    double SectionGap = 96,
    double RoundHeaderBandHeight = 28,
    double MetaCaptionHeight = 14,
    double MatchVerticalGap = 12,
    double? UnitHeightOverride = null,
    bool MiniBracket = false);

public sealed record PositionedBracketNode(
    Guid Id,
    BracketBranchDto Branch,
    int Round,
    int Slot,
    double X,
    double Y,
    double Width,
    double Height);

public sealed record BracketConnector(
    Guid FromNodeId,
    Guid ToNodeId,
    BracketConnectorKind Kind,
    string Path);

public sealed record BracketJunction(
    double X,
    double Y,
    BracketConnectorKind Kind);

public enum BracketConnectorKind
{
    WinsTo = 1,
    LosesTo = 2
}

public sealed record BracketLayoutSection(
    BracketBranchDto Branch,
    double X,
    double Y,
    double Width,
    double Height,
    string? Title = null,
    string? Subtitle = null,
    bool HasDividerAbove = false);

public sealed record BracketRoundColumn(
    BracketBranchDto Branch,
    int Round,
    double X,
    double Width,
    string Label,
    double HeaderY);

public sealed record BracketLayoutResult(
    IReadOnlyList<PositionedBracketNode> Nodes,
    IReadOnlyList<BracketConnector> Connectors,
    IReadOnlyList<BracketJunction> Junctions,
    IReadOnlyList<BracketRoundColumn> RoundColumns,
    IReadOnlyDictionary<BracketBranchDto, BracketLayoutSection> Sections,
    double CanvasWidth,
    double CanvasHeight,
    double RoundHeaderBandHeight);

public sealed class BracketLayoutEngine
{
    private const double BandTitleHeight = 24;
    private const double BandDividerHeight = 16;
    private const double BandBottomPadding = 8;

    public BracketLayoutResult Layout(
        IReadOnlyCollection<BracketLayoutNodeInput> nodes,
        BracketLayoutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        options ??= new BracketLayoutOptions();
        Validate(nodes, options);

        if (nodes.Count == 0)
        {
            return new BracketLayoutResult(
                Array.Empty<PositionedBracketNode>(),
                Array.Empty<BracketConnector>(),
                Array.Empty<BracketJunction>(),
                Array.Empty<BracketRoundColumn>(),
                new Dictionary<BracketBranchDto, BracketLayoutSection>(),
                0,
                0,
                options.RoundHeaderBandHeight);
        }

        var branchLayouts = nodes
            .GroupBy(node => node.Branch)
            .ToDictionary(
                group => group.Key,
                group => LayoutBranch(group.ToArray(), options));

        var isBanded = !options.MiniBracket && branchLayouts.ContainsKey(BracketBranchDto.Losers);
        var offsets = CalculateBranchOffsets(branchLayouts, options, isBanded, out var bandTops);
        var matchTopInset = BandTopChrome(isBanded, options);

        var positionedNodes = branchLayouts
            .SelectMany(pair =>
            {
                var offset = offsets[pair.Key];
                return pair.Value.Nodes.Select(node => node with
                {
                    X = node.X + offset.X,
                    Y = node.Y + offset.Y
                });
            })
            .OrderBy(node => node.Branch)
            .ThenBy(node => node.Round)
            .ThenBy(node => node.Slot)
            .ToArray();

        var minX = positionedNodes.Min(node => node.X);
        var minY = positionedNodes.Min(node => node.Y);
        var normalizeX = minX < 0 ? -minX : 0;
        var normalizeY = minY < 0 ? -minY : 0;

        if (normalizeX > 0 || normalizeY > 0)
        {
            positionedNodes = positionedNodes
                .Select(node => node with
                {
                    X = node.X + normalizeX,
                    Y = node.Y + normalizeY
                })
                .ToArray();

            offsets = offsets.ToDictionary(
                pair => pair.Key,
                pair => (pair.Value.X + normalizeX, pair.Value.Y + normalizeY));

            bandTops = bandTops.ToDictionary(
                pair => pair.Key,
                pair => pair.Value + normalizeY);
        }

        var nodeById = positionedNodes.ToDictionary(node => node.Id);
        var inputById = nodes.ToDictionary(node => node.Id);
        var connectors = positionedNodes
            .SelectMany(node => CreateConnectors(node, inputById[node.Id], nodeById))
            .ToArray();

        var junctions = CreateJunctions(connectors, nodeById);

        var sections = branchLayouts.ToDictionary(
            pair => pair.Key,
            pair =>
            {
                var offset = offsets[pair.Key];
                var bandTop = bandTops[pair.Key];
                var layout = pair.Value;
                var bandHeight = Math.Max(
                    matchTopInset + layout.Height + BandBottomPadding,
                    offset.Y + layout.Height + BandBottomPadding - bandTop);
                return new BracketLayoutSection(
                    pair.Key,
                    offset.X,
                    bandTop,
                    layout.Width,
                    bandHeight,
                    SectionTitle(pair.Key, isBanded),
                    SectionSubtitle(pair.Key, isBanded),
                    HasDividerAbove(pair.Key, isBanded));
            });

        var roundColumns = BuildRoundColumns(positionedNodes, sections, inputs: nodes, options, isBanded);

        var canvasWidth = positionedNodes.Max(node => node.X + node.Width);
        var canvasHeight = positionedNodes.Max(node => node.Y + node.Height);

        return new BracketLayoutResult(
            positionedNodes,
            connectors,
            junctions,
            roundColumns,
            sections,
            canvasWidth,
            canvasHeight,
            options.RoundHeaderBandHeight);
    }

    private static BranchLayout LayoutBranch(
        IReadOnlyCollection<BracketLayoutNodeInput> nodes,
        BracketLayoutOptions options)
    {
        var totalRounds = nodes.Max(node => node.Round);
        var leafRowSpan = Math.Pow(2, Math.Max(0, totalRounds - 1));
        var effectiveMatchHeight = options.CardHeight + options.MetaCaptionHeight;
        var unitHeight = options.UnitHeightOverride
            ?? (effectiveMatchHeight + options.MatchVerticalGap) / leafRowSpan;

        var yById = new Dictionary<Guid, double>();

        foreach (var node in nodes.Where(n => n.Round == 1).OrderBy(n => n.Slot))
        {
            var rowSpan = Math.Pow(2, totalRounds - 1);
            var y = node.Slot * rowSpan * unitHeight
                + (rowSpan - 1) * unitHeight / 2;
            yById[node.Id] = y;
        }

        for (var round = 2; round <= totalRounds; round++)
        {
            foreach (var node in nodes.Where(n => n.Round == round).OrderBy(n => n.Slot))
            {
                var feeders = nodes
                    .Where(n => n.WinsToNodeId == node.Id)
                    .OrderBy(n => n.Slot)
                    .Select(n => yById[n.Id])
                    .ToList();

                yById[node.Id] = feeders.Count switch
                {
                    0 => node.Slot * unitHeight,
                    1 => feeders[0],
                    _ => (feeders[0] + feeders[1]) / 2
                };
            }
        }

        var positionedNodes = nodes
            .Select(node =>
            {
                var x = (node.Round - 1) * (options.CardWidth + options.ConnectorGap);
                return new PositionedBracketNode(
                    node.Id,
                    node.Branch,
                    node.Round,
                    node.Slot,
                    x,
                    yById[node.Id],
                    options.CardWidth,
                    effectiveMatchHeight);
            })
            .OrderBy(node => node.Round)
            .ThenBy(node => node.Slot)
            .ToArray();

        var width = positionedNodes.Max(node => node.X + node.Width);
        var height = Math.Max(
            positionedNodes.Max(node => node.Y + node.Height),
            leafRowSpan * unitHeight);

        return new BranchLayout(positionedNodes, width, height);
    }

    private static double BandTopChrome(bool isBanded, BracketLayoutOptions options) =>
        (isBanded ? BandTitleHeight : 0) + options.RoundHeaderBandHeight;

    private static string? SectionTitle(BracketBranchDto branch, bool isBanded) => isBanded
        ? branch switch
        {
            BracketBranchDto.Winners => "Winners bracket",
            BracketBranchDto.Losers => "Losers bracket",
            BracketBranchDto.GrandFinal => "Grand final",
            BracketBranchDto.ThirdPlace => "Third place",
            _ => null
        }
        : null;

    private static string? SectionSubtitle(BracketBranchDto branch, bool isBanded) =>
        isBanded && branch == BracketBranchDto.Losers
            ? "Lower bracket — extra games for eliminated teams"
            : null;

    private static bool HasDividerAbove(BracketBranchDto branch, bool isBanded) =>
        isBanded && branch == BracketBranchDto.Losers;

    private static IReadOnlyList<BracketRoundColumn> BuildRoundColumns(
        IReadOnlyList<PositionedBracketNode> positionedNodes,
        IReadOnlyDictionary<BracketBranchDto, BracketLayoutSection> sections,
        IReadOnlyCollection<BracketLayoutNodeInput> inputs,
        BracketLayoutOptions options,
        bool isBanded)
    {
        var maxRoundByBranch = inputs
            .GroupBy(n => n.Branch)
            .ToDictionary(g => g.Key, g => g.Max(n => n.Round));

        return positionedNodes
            .GroupBy(n => (n.Branch, n.Round))
            .Select(group =>
            {
                var branch = group.Key.Branch;
                var round = group.Key.Round;
                var maxRound = maxRoundByBranch[branch];
                var label = branch switch
                {
                    BracketBranchDto.GrandFinal => "Grand final",
                    BracketBranchDto.ThirdPlace => "Third place",
                    BracketBranchDto.Winners when round == maxRound => "Final",
                    _ => $"Round {round}"
                };

                var section = sections[branch];
                var headerY = section.Y + (isBanded ? BandTitleHeight : 0);

                return new BracketRoundColumn(
                    branch,
                    round,
                    group.Min(n => n.X),
                    options.CardWidth,
                    label,
                    headerY);
            })
            .OrderBy(c => c.HeaderY)
            .ThenBy(c => c.X)
            .ThenBy(c => c.Round)
            .ToList();
    }

    private static Dictionary<BracketBranchDto, (double X, double Y)> CalculateBranchOffsets(
        IReadOnlyDictionary<BracketBranchDto, BranchLayout> branches,
        BracketLayoutOptions options,
        bool isBanded,
        out Dictionary<BracketBranchDto, double> bandTops)
    {
        var offsets = new Dictionary<BracketBranchDto, (double X, double Y)>();
        bandTops = new Dictionary<BracketBranchDto, double>();
        var matchTopInset = BandTopChrome(isBanded, options);

        var currentBandTop = 0d;

        if (branches.TryGetValue(BracketBranchDto.Winners, out var winners))
        {
            bandTops[BracketBranchDto.Winners] = currentBandTop;
            offsets[BracketBranchDto.Winners] = (0, currentBandTop + matchTopInset);
            currentBandTop += matchTopInset + winners.Height + BandBottomPadding;
        }

        if (branches.TryGetValue(BracketBranchDto.Losers, out var losers))
        {
            currentBandTop += options.SectionGap + (isBanded ? BandDividerHeight : 0);
            bandTops[BracketBranchDto.Losers] = currentBandTop;
            offsets[BracketBranchDto.Losers] = (0, currentBandTop + matchTopInset);
            currentBandTop += matchTopInset + losers.Height + BandBottomPadding;
        }

        AddGrandFinalOffset(branches, offsets, bandTops, options, isBanded, matchTopInset);

        var stackedY = currentBandTop;
        foreach (var branch in branches.Keys
            .Where(branch => branch is not BracketBranchDto.Winners
                and not BracketBranchDto.Losers
                and not BracketBranchDto.GrandFinal)
            .OrderBy(branch => branch))
        {
            if (stackedY > 0)
            {
                stackedY += options.SectionGap + (isBanded ? BandDividerHeight : 0);
            }

            bandTops[branch] = stackedY;
            offsets[branch] = (0, stackedY + matchTopInset);
            stackedY += matchTopInset + branches[branch].Height + BandBottomPadding;
        }

        return offsets;
    }

    private static void AddGrandFinalOffset(
        IReadOnlyDictionary<BracketBranchDto, BranchLayout> branches,
        IDictionary<BracketBranchDto, (double X, double Y)> offsets,
        IDictionary<BracketBranchDto, double> bandTops,
        BracketLayoutOptions options,
        bool isBanded,
        double matchTopInset)
    {
        if (!branches.TryGetValue(BracketBranchDto.GrandFinal, out var grandFinal))
        {
            return;
        }

        var rightEdge = branches
            .Where(pair => pair.Key is BracketBranchDto.Winners or BracketBranchDto.Losers)
            .Select(pair => pair.Value.Width)
            .DefaultIfEmpty(0)
            .Max();

        var x = rightEdge + options.ConnectorGap;
        var y = 0d;

        if (offsets.TryGetValue(BracketBranchDto.Winners, out var winnersOffset)
            && offsets.TryGetValue(BracketBranchDto.Losers, out var losersOffset))
        {
            var winnersFinalCenter = FindFinalCenter(
                branches[BracketBranchDto.Winners],
                winnersOffset.Y);
            var losersFinalCenter = FindFinalCenter(
                branches[BracketBranchDto.Losers],
                losersOffset.Y);
            var center = (winnersFinalCenter + losersFinalCenter) / 2;
            y = center - grandFinal.Height / 2;
        }
        else if (offsets.TryGetValue(BracketBranchDto.Winners, out winnersOffset))
        {
            var center = FindFinalCenter(branches[BracketBranchDto.Winners], winnersOffset.Y);
            y = center - grandFinal.Height / 2;
        }

        var bandTop = bandTops.TryGetValue(BracketBranchDto.Winners, out var winnersBandTop)
            ? winnersBandTop
            : Math.Max(0, y - matchTopInset);

        bandTops[BracketBranchDto.GrandFinal] = bandTop;
        offsets[BracketBranchDto.GrandFinal] = (x, y);
    }

    private static double FindFinalCenter(BranchLayout branch, double yOffset)
    {
        var final = branch.Nodes
            .OrderByDescending(node => node.Round)
            .ThenBy(node => node.Slot)
            .First();

        return yOffset + final.Y + final.Height / 2;
    }

    private static IEnumerable<BracketConnector> CreateConnectors(
        PositionedBracketNode node,
        BracketLayoutNodeInput input,
        IReadOnlyDictionary<Guid, PositionedBracketNode> nodeById)
    {
        if (input.WinsToNodeId is { } winsToNodeId
            && nodeById.TryGetValue(winsToNodeId, out var winsToNode))
        {
            yield return new BracketConnector(
                node.Id,
                winsToNodeId,
                BracketConnectorKind.WinsTo,
                CreateElbowPath(node, winsToNode));
        }

        if (input.LosesToNodeId is { } losesToNodeId
            && nodeById.TryGetValue(losesToNodeId, out var losesToNode))
        {
            yield return new BracketConnector(
                node.Id,
                losesToNodeId,
                BracketConnectorKind.LosesTo,
                CreateElbowPath(node, losesToNode));
        }
    }

    private static IReadOnlyList<BracketJunction> CreateJunctions(
        IReadOnlyList<BracketConnector> connectors,
        IReadOnlyDictionary<Guid, PositionedBracketNode> nodeById)
    {
        var junctions = new List<BracketJunction>();
        var seen = new HashSet<(double X, double Y, BracketConnectorKind Kind)>();

        foreach (var group in connectors.GroupBy(c => (c.ToNodeId, c.Kind)))
        {
            if (!nodeById.TryGetValue(group.Key.ToNodeId, out var parent))
            {
                continue;
            }

            foreach (var connector in group)
            {
                if (!nodeById.TryGetValue(connector.FromNodeId, out var child))
                {
                    continue;
                }

                var startX = child.X + child.Width;
                var endX = parent.X;
                var endY = parent.Y + parent.Height / 2;
                var midX = startX + (endX - startX) / 2;
                var key = (Math.Round(midX, 3), Math.Round(endY, 3), connector.Kind);

                if (seen.Add(key))
                {
                    junctions.Add(new BracketJunction(midX, endY, connector.Kind));
                }
            }
        }

        return junctions;
    }

    private static string CreateElbowPath(PositionedBracketNode from, PositionedBracketNode to)
    {
        var startX = from.X + from.Width;
        var startY = from.Y + from.Height / 2;
        var endX = to.X;
        var endY = to.Y + to.Height / 2;
        var midX = startX + (endX - startX) / 2;

        return FormattableString.Invariant($"M {startX:0.###} {startY:0.###} H {midX:0.###} V {endY:0.###} H {endX:0.###}");
    }

    private static void Validate(
        IReadOnlyCollection<BracketLayoutNodeInput> nodes,
        BracketLayoutOptions options)
    {
        if (options.CardWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Card width must be greater than zero.");
        }

        if (options.CardHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Card height must be greater than zero.");
        }

        if (options.ConnectorGap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Connector gap must be zero or greater.");
        }

        if (options.MatchVerticalGap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Match vertical gap must be zero or greater.");
        }

        if (options.UnitHeightOverride is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Unit height override must be greater than zero when set.");
        }

        if (options.SectionGap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Section gap must be zero or greater.");
        }

        var duplicateNodeId = nodes
            .GroupBy(node => node.Id)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicateNodeId is { } id)
        {
            throw new ArgumentException($"Duplicate bracket layout node id '{id}'.", nameof(nodes));
        }

        foreach (var node in nodes)
        {
            if (node.Round < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(nodes), "Bracket layout rounds are 1-based.");
            }

            if (node.Slot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nodes), "Bracket layout slots are 0-based.");
            }
        }
    }

    private sealed record BranchLayout(
        IReadOnlyList<PositionedBracketNode> Nodes,
        double Width,
        double Height);
}
