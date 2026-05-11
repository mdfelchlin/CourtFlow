namespace CourtFlow.Web.Shared.Services;

/// <summary>Deterministic division chip colors via CSS <c>--cf-division-*</c> tokens (see <c>docs/ui/design-system.md</c>).</summary>
public static class DivisionColor
{
    public readonly record struct Pair(string Background, string Foreground);

    private static readonly string[] Backgrounds =
    [
        "var(--cf-division-1-bg)", "var(--cf-division-2-bg)", "var(--cf-division-3-bg)", "var(--cf-division-4-bg)",
        "var(--cf-division-5-bg)", "var(--cf-division-6-bg)", "var(--cf-division-7-bg)", "var(--cf-division-8-bg)"
    ];

    private static readonly string[] Foregrounds =
    [
        "var(--cf-division-1-fg)", "var(--cf-division-2-fg)", "var(--cf-division-3-fg)", "var(--cf-division-4-fg)",
        "var(--cf-division-5-fg)", "var(--cf-division-6-fg)", "var(--cf-division-7-fg)", "var(--cf-division-8-fg)"
    ];

    /// <summary>Returns CSS-variable pairs for MudBlazor <c>Style</c> bindings (never inline literals).</summary>
    public static Pair For(Guid divisionId)
    {
        var ix = StableIndex(divisionId, Backgrounds.Length);
        return new Pair(Backgrounds[ix], Foregrounds[ix]);
    }

    public static string Style(Guid divisionId)
    {
        var p = For(divisionId);
        return $"background:{p.Background};color:{p.Foreground};";
    }

    private static int StableIndex(Guid id, int mod)
    {
        Span<byte> b = stackalloc byte[16];
        _ = id.TryWriteBytes(b);
        unchecked
        {
            var h = Fnv1aHash(b);
            return (int)(h % (uint)mod);
        }
    }

    private static uint Fnv1aHash(ReadOnlySpan<byte> data)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        var hash = offsetBasis;
        foreach (var by in data)
        {
            hash ^= by;
            hash *= prime;
        }
        return hash;
    }
}
