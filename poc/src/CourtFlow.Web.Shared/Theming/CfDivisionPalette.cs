namespace CourtFlow.Web.Shared.Theming;

public static class CfDivisionPalette
{
    /// <summary>Returns a pastel HSLA background string for tagging UI by division identity.</summary>
    public static string SoftBackgroundCss(Guid divisionId)
    {
        var h = HueFromGuid(divisionId);
        return $"hsla({h}, 72%, 42%, 0.18)";
    }

    static int HueFromGuid(Guid id)
    {
        var bytes = id.ToByteArray();
        var n = BitConverter.ToUInt64(bytes, 0);
        var n2 = BitConverter.ToUInt64(bytes.AsSpan()[8..]);
        return (int)((n ^ n2) % 360);
    }
}
