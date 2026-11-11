using System.Globalization;

namespace PokeArena.Web.Services;

/// <summary>
/// One bar of a ranking (<c>Shared/BarList.razor</c>): the „Schaden“ box of a fight page (feat/damage-meter) and the
/// lists of the statistics page (feat/statistics-page). Name and value sit inside the bar like the HP numbers.
/// </summary>
/// <param name="Rank">„1.“; equal values share a rank. Null for bars without a place (the totals).</param>
/// <param name="Name">Bold part: a trainer, a Pokémon or a side.</param>
/// <param name="Detail">Plain part after the name; null for none.</param>
/// <param name="Value">The number at the right end, as shown.</param>
/// <param name="Share">Bar length in percent of the longest bar of the list, invariant „0.#“.</param>
/// <param name="Fill">Bar colour, <c>#RRGGBB</c>: <see cref="Tint.Darkened"/>, the colour names have on the site.</param>
/// <param name="Href">Relative link of the name (a trainer page); null for plain text.</param>
public sealed record Bar(string? Rank, string Name, string? Detail, string Value, string Share, string Fill, string? Href = null)
{
    /// <summary>A bar without a usable colour: a trainer without a chat colour, a deleted trainer.</summary>
    public const string Neutral = "#6b6b76";

    /// <summary>A bar for several colours at once (a side of several trainers, a pair of rivals): the HP bar's green.</summary>
    public const string Mixed = "#1b7f45";

    /// <summary>
    /// A bar of length <paramref name="amount"/> / <paramref name="longest"/>, its <paramref name="color"/> darkened
    /// like the names; a colour that is not <c>#RRGGBB</c> becomes <see cref="Neutral"/>, so only checked values reach
    /// the inline style.
    /// </summary>
    public static Bar Make(string? rank, string name, string? detail, string value, long amount, long longest, string? color, string? href = null) =>
        new(rank, name, detail, value,
            (longest > 0 ? 100.0 * Math.Clamp(amount, 0, longest) / longest : 0).ToString("0.#", CultureInfo.InvariantCulture),
            Tint.Darkened(Tint.IsColor(color) ? color!.ToLowerInvariant() : Neutral),
            href);

    public static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
