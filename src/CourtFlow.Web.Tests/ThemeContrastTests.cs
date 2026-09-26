using CourtFlow.Web.Theming;
using MudBlazor;
using MudBlazor.Utilities;

namespace CourtFlow.Web.Tests;

/// <summary>Every text-on-color pair in both palettes reaches WCAG AA, 4.5:1.</summary>
public class ThemeContrastTests
{
    private const double MinimumContrast = 4.5;

    public static TheoryData<string> Modes => ["light", "dark"];

    [Theory]
    [MemberData(nameof(Modes))]
    public void Text_reads_on_background_and_surface(string mode)
    {
        var palette = Palette(mode);

        AssertContrast(palette.TextPrimary, palette.Background, "text primary on background");
        AssertContrast(palette.TextPrimary, palette.Surface, "text primary on surface");
        AssertContrast(palette.TextSecondary, palette.Background, "text secondary on background");
        AssertContrast(palette.TextSecondary, palette.Surface, "text secondary on surface");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void App_bar_text_reads_on_the_app_bar(string mode)
    {
        var palette = Palette(mode);

        AssertContrast(palette.AppbarText, palette.AppbarBackground, "app bar text");
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Each_status_color_reads_against_its_contrast_text(string mode)
    {
        var palette = Palette(mode);

        AssertContrast(palette.PrimaryContrastText, palette.Primary, "primary");
        AssertContrast(palette.SecondaryContrastText, palette.Secondary, "secondary");
        AssertContrast(palette.SuccessContrastText, palette.Success, "success");
        AssertContrast(palette.InfoContrastText, palette.Info, "info");
        AssertContrast(palette.WarningContrastText, palette.Warning, "warning");
        AssertContrast(palette.ErrorContrastText, palette.Error, "error");
    }

    [Fact]
    public void Drawer_text_reads_on_the_dark_drawer()
    {
        var palette = CourtFlowTheme.Theme.PaletteDark;

        AssertContrast(palette.DrawerText, palette.DrawerBackground, "drawer text");
    }

    /// <summary>MudBlazor uses Primary as text for links and the active drawer item.</summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public void Primary_reads_as_text_on_every_surface(string mode)
    {
        var palette = Palette(mode);

        AssertContrast(palette.Primary, palette.Background, "primary text on background");
        AssertContrast(palette.Primary, palette.Surface, "primary text on surface");
        AssertContrast(palette.Primary, palette.DrawerBackground, "primary text on drawer");
    }

    private static Palette Palette(string mode) =>
        mode == "dark" ? CourtFlowTheme.Theme.PaletteDark : CourtFlowTheme.Theme.PaletteLight;

    private static void AssertContrast(MudColor foreground, MudColor background, string pair)
    {
        var ratio = Contrast(foreground, background);

        Assert.True(ratio >= MinimumContrast, $"{pair}: {foreground.Value} on {background.Value} is {ratio:F2}:1, below {MinimumContrast}:1.");
    }

    private static double Contrast(MudColor a, MudColor b)
    {
        var lighter = Math.Max(Luminance(a), Luminance(b));
        var darker = Math.Min(Luminance(a), Luminance(b));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(MudColor color) =>
        0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);

    private static double Channel(byte value)
    {
        var s = value / 255.0;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
}
