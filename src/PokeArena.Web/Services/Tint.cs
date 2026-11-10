using System.Globalization;
using PokeArena.Contracts;

namespace PokeArena.Web.Services;

/// <summary>
/// Text colours from colours nobody chose for readability — a trainer's Twitch chat colour, a boss's type colour.
/// Each is darkened for the light theme and lightened for the dark theme until it reaches WCAG AA (4.5 : 1) on the
/// page surface; a colour that already passes stays as it is.
/// </summary>
public static class Tint
{
    /// <summary>Contrast every tinted text reaches on <see cref="LightSurface"/> and <see cref="DarkSurface"/>.</summary>
    public const double MinContrast = 4.5;

    /// <summary><c>--surface</c> of the light theme in site.css (fight sides and log).</summary>
    public const string LightSurface = "#ffffff";

    /// <summary><c>--surface</c> of the dark theme in site.css.</summary>
    public const string DarkSurface = "#222228";

    /// <summary>
    /// Chip colours of the types (<c>.type-*</c> in site.css; a test keeps both in line), keyed by
    /// <see cref="Format.TypeClass"/>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> TypeColors = new Dictionary<string, string>
    {
        ["type-normal"] = "#8f8f78",
        ["type-feuer"] = "#e2671f",
        ["type-wasser"] = "#4d7fe0",
        ["type-pflanze"] = "#5aa63c",
        ["type-elektro"] = "#c9a10a",
        ["type-eis"] = "#4fb3b3",
        ["type-kampf"] = "#b02c25",
        ["type-gift"] = "#8f3e94",
        ["type-boden"] = "#b8913c",
        ["type-flug"] = "#8670d8",
        ["type-psycho"] = "#e5487a",
        ["type-kaefer"] = "#8a9a17",
        ["type-gestein"] = "#9c8730",
        ["type-geist"] = "#5e4b88",
        ["type-drache"] = "#5b2ff0",
    };

    /// <summary>
    /// The colour of one part of a fighter's label in the log: a boss's whole label in the colour of its first type
    /// (the Pokémon is the element), a trainer's name in their chat colour; null for everything else (species and HP
    /// of trainers' Pokémon, NPCs, trainers without a known colour).
    /// </summary>
    /// <param name="side">Index of the fighter's side on the page: the boss is the only fighter on side 1. Never tell the
    /// boss by a missing trainer id — a deleted trainer's fighters have none either (review feat/fight-log-colours).</param>
    /// <param name="isOwner">The part is the owner („Ash“, „Boss“), not the species or HP around it.</param>
    public static string? Of(BattlePage battle, int side, BattleFighter fighter, bool isOwner) =>
        battle.Kind == BattleKind.Boss && side == 1
            ? fighter.Species.Types.Count > 0 ? TypeColors.GetValueOrDefault(Format.TypeClass(fighter.Species.Types[0])) : null
            : isOwner ? fighter.Color : null;

    /// <summary>Inline style for the <c>tinted</c> class: both theme variants of <paramref name="color"/>; null if it is not <c>#RRGGBB</c>.</summary>
    public static string? Style(string? color) =>
        color is not null && TryParse(color, out var rgb)
            ? $"--tint-light: {ToHex(Readable(rgb, Parse(LightSurface), towards: 0))}; --tint-dark: {ToHex(Readable(rgb, Parse(DarkSurface), towards: 255))}"
            : null;

    /// <summary>WCAG contrast ratio of two <c>#RRGGBB</c> colours (1 … 21).</summary>
    public static double Contrast(string a, string b) => Contrast(Parse(a), Parse(b));

    /// <summary><paramref name="color"/> mixed towards black or white in 5 % steps until it passes on <paramref name="surface"/>.</summary>
    private static (int R, int G, int B) Readable((int R, int G, int B) color, (int R, int G, int B) surface, int towards)
    {
        for (var step = 0; step <= 20; step++)
        {
            var t = step / 20.0;
            var mixed = (Mix(color.R, towards, t), Mix(color.G, towards, t), Mix(color.B, towards, t));
            if (Contrast(mixed, surface) >= MinContrast)
            {
                return mixed;
            }
        }

        return (towards, towards, towards);
    }

    private static int Mix(int from, int to, double t) => (int)Math.Round(from + ((to - from) * t));

    private static double Contrast((int R, int G, int B) a, (int R, int G, int B) b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance((int R, int G, int B) c) =>
        (0.2126 * Linear(c.R)) + (0.7152 * Linear(c.G)) + (0.0722 * Linear(c.B));

    private static double Linear(int channel)
    {
        var s = channel / 255.0;
        return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }

    private static bool TryParse(string hex, out (int R, int G, int B) rgb)
    {
        rgb = default;
        if (hex.Length != 7 || hex[0] != '#' || !int.TryParse(hex.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        rgb = ((value >> 16) & 0xff, (value >> 8) & 0xff, value & 0xff);
        return true;
    }

    private static (int R, int G, int B) Parse(string hex) =>
        TryParse(hex, out var rgb) ? rgb : throw new FormatException($"Not a #RRGGBB colour: {hex}");

    private static string ToHex((int R, int G, int B) c) => FormattableString.Invariant($"#{c.R:x2}{c.G:x2}{c.B:x2}");
}
