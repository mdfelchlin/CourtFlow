using MudBlazor;

namespace CourtFlow.Web.Theming;

/// <summary>Typed component defaults the Cf wrappers and the layout read, so pages never repeat them.</summary>
public static class CfDefaults
{
    public const Variant FieldVariant = Variant.Outlined;
    public const Margin FieldMargin = Margin.Dense;
    public const Size ButtonSize = Size.Small;

    /// <summary>
    /// Filled, so alert text is the status color's contrast text. MudBlazor's text and outlined
    /// variants use the status color itself as text, which fails contrast on the dark surface.
    /// </summary>
    public const Variant AlertVariant = Variant.Filled;
    public const int AppBarElevation = 1;
    public const MaxWidth ContentMaxWidth = MaxWidth.ExtraExtraLarge;
    public const MaxWidth DialogMaxWidth = MaxWidth.Small;
}
