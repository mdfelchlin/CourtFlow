using System.Globalization;

namespace CourtFlow.Web;

/// <summary>
/// Director-facing calendar dates: month, day, then year, with literal slashes.
/// The quoted slashes and invariant culture keep the text stable when the browser locale is day-first.
/// </summary>
public static class CalendarDateFormat
{
    public const string Pattern = "MM'/'dd'/'yyyy";

    /// <summary>
    /// Typed entry. <c>M</c> and <c>d</c> accept one or two digits; <c>yyyy</c> requires four.
    /// Display still uses <see cref="Pattern"/> so the field shows leading zeros.
    /// </summary>
    public const string TypedPattern = "M'/'d'/'yyyy";

    public const string Placeholder = "mm/dd/yyyy";

    public static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Display(DateOnly date) => date.ToString(Pattern, Culture);

    /// <summary>The calendar date of an instant in the browser's time zone.</summary>
    public static string Display(DateTimeOffset instant) => Display(DateOnly.FromDateTime(instant.LocalDateTime));

    public static bool TryParse(string? text, out DateOnly date)
    {
        if (DateTime.TryParseExact(text, TypedPattern, Culture, DateTimeStyles.None, out var parsed))
        {
            date = DateOnly.FromDateTime(parsed);
            return true;
        }

        date = default;
        return false;
    }
}
