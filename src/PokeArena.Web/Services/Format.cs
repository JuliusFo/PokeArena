using System.Globalization;

namespace PokeArena.Web.Services;

/// <summary>German date and type formatting for the pages.</summary>
public static class Format
{
    private static readonly TimeZoneInfo Local = FindGermanTime();

    /// <summary>„01.11.2026, 18:30 Uhr“ in German time, whatever the visitor's browser says.</summary>
    public static string DateTime(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, Local).ToString("dd.MM.yyyy, HH:mm 'Uhr'", CultureInfo.InvariantCulture);

    public static string Time(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, Local).ToString("HH:mm 'Uhr'", CultureInfo.InvariantCulture);

    public static string Date(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, Local).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>CSS class of a German type name: „Käfer“ → <c>type-kaefer</c>.</summary>
    public static string TypeClass(string type) =>
        "type-" + type.ToLowerInvariant().Replace("ä", "ae", StringComparison.Ordinal).Replace("ö", "oe", StringComparison.Ordinal).Replace("ü", "ue", StringComparison.Ordinal);

    /// <summary>„18:30 Uhr“ on the same day as <paramref name="now"/>, otherwise with the date (review 1.0.3, A34).</summary>
    public static string Moment(DateTimeOffset value, DateTimeOffset now) =>
        Date(value) == Date(now) ? Time(value) : DateTime(value);

    private static TimeZoneInfo FindGermanTime()
    {
        // WebAssembly ships the IANA zone data; fall back to UTC if a trimmed build lacks it.
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
