using System.Globalization;

namespace PokeArena.Web.Services;

/// <summary>
/// The colour of HP by what is left, over the whole range (claude/health-bar-gradient-166dce): red only at 0, yellow at
/// half, green only when full, every HP in between its own shade. Before (claude/hp-bar-color-gradient-ee5d5b) red up to
/// a quarter and green from three quarters through orange at half — viewers read that as a few fixed colours, half the
/// range was one of two (user decision 2026-11-28, after a friend's mockup that runs the hue evenly from green over
/// yellow to red; it replaces „Grün schon ab ¾“ of 2026-11-27). Mixed in OKLCH, which keeps lightness and saturation
/// even (a mix in sRGB or on OKLab's straight line turned the middle greyish brown). Bars and text share it:
/// <see cref="Fill"/> is the bar's fill under white numbers and the HP text on the light page, so its yellow is a dark
/// ochre; <see cref="OnDark"/> the HP text on the dark page, bright. <c>HpColorTests</c> checks 4.5 : 1 along the whole way.
/// </summary>
public static class HpColor
{
    private static readonly Stops FillStops = new("#c42d0b", "#8a6d00", "#1b7f45");
    private static readonly Stops DarkStops = new("#ff8a6e", "#e2c044", "#4cc283");

    /// <summary>Fill of an HP bar (white numbers, both themes) and HP text on the light page.</summary>
    public static string Fill(int hp, int max) => FillStops.At(Share(hp, max));

    /// <summary>HP text on the dark page.</summary>
    public static string OnDark(int hp, int max) => DarkStops.At(Share(hp, max));

    /// <summary>The inline style of coloured HP text: the colour for each theme, picked in site.css (<c>.log-hp</c>).</summary>
    public static string TextStyle(int hp, int max) => $"--log-hp: {Fill(hp, max)}; --log-hp-dark: {OnDark(hp, max)}";

    /// <summary>
    /// What is left, 0 … 1. In <see cref="double"/>: a file not ours with HP near <see cref="int.MaxValue"/> must not wrap
    /// (review claude/hp-coloring-bar-view-4f3c63). A maximum of 0 or less counts as 1, like the bar's width.
    /// </summary>
    internal static double Share(int hp, int max)
    {
        var top = Math.Max(1, max);
        return Math.Clamp(hp, 0, top) / (double)top;
    }

    /// <summary>Red, yellow and green in OKLab, mixed in OKLCH by <see cref="At"/>.</summary>
    private sealed class Stops(string low, string mid, string high)
    {
        private readonly (double L, double A, double B) _low = ToOklab(low);
        private readonly (double L, double A, double B) _mid = ToOklab(mid);
        private readonly (double L, double A, double B) _high = ToOklab(high);

        public string At(double share) => share switch
        {
            <= 0 => ToHex(_low),
            < 0.5 => ToHex(Mix(_low, _mid, share / 0.5)),
            < 1 => ToHex(Mix(_mid, _high, (share - 0.5) / 0.5)),
            _ => ToHex(_high),
        };

        /// <summary>Lightness, chroma and hue each on their own (OKLCH), the hue the short way round.</summary>
        private static (double, double, double) Mix((double L, double A, double B) from, (double L, double A, double B) to, double t)
        {
            var (fromChroma, toChroma) = (Math.Sqrt((from.A * from.A) + (from.B * from.B)), Math.Sqrt((to.A * to.A) + (to.B * to.B)));
            var fromHue = Math.Atan2(from.B, from.A);
            var turn = Math.Atan2(to.B, to.A) - fromHue;
            turn += turn > Math.PI ? -2 * Math.PI : turn < -Math.PI ? 2 * Math.PI : 0;
            var (chroma, hue) = (fromChroma + ((toChroma - fromChroma) * t), fromHue + (turn * t));
            return (from.L + ((to.L - from.L) * t), chroma * Math.Cos(hue), chroma * Math.Sin(hue));
        }
    }

    // OKLab after Björn Ottosson (2020), from and to sRGB.
    private static (double L, double A, double B) ToOklab(string hex)
    {
        var value = int.Parse(hex.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        var (r, g, b) = (ToLinear((value >> 16) & 0xff), ToLinear((value >> 8) & 0xff), ToLinear(value & 0xff));
        var l = Math.Cbrt((0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * b));
        var m = Math.Cbrt((0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * b));
        var s = Math.Cbrt((0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * b));
        return (
            (0.2104542553 * l) + (0.7936177850 * m) - (0.0040720468 * s),
            (1.9779984951 * l) - (2.4285922050 * m) + (0.4505937099 * s),
            (0.0259040371 * l) + (0.7827717662 * m) - (0.8086757660 * s));
    }

    private static string ToHex((double L, double A, double B) lab)
    {
        var l = Math.Pow(lab.L + (0.3963377774 * lab.A) + (0.2158037573 * lab.B), 3);
        var m = Math.Pow(lab.L - (0.1055613458 * lab.A) - (0.0638541728 * lab.B), 3);
        var s = Math.Pow(lab.L - (0.0894841775 * lab.A) - (1.2914855480 * lab.B), 3);
        var r = (4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s);
        var g = (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s);
        var b = (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076926490 * s);
        return FormattableString.Invariant($"#{ToChannel(r):x2}{ToChannel(g):x2}{ToChannel(b):x2}");
    }

    private static double ToLinear(int channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    /// <summary>Back to 0 … 255; a mix just outside sRGB is clamped.</summary>
    private static int ToChannel(double linear)
    {
        var c = Math.Clamp(linear, 0, 1);
        var encoded = c <= 0.0031308 ? 12.92 * c : (1.055 * Math.Pow(c, 1 / 2.4)) - 0.055;
        return (int)Math.Round(encoded * 255);
    }
}
