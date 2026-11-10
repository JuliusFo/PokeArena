using System.Text.RegularExpressions;
using PokeArena.Web.Services;

namespace PokeArena.Web.Tests;

public partial class TintTests
{
    [Theory]
    [InlineData("#FFFF00")] // yellow: unreadable on white
    [InlineData("#0000FF")]
    [InlineData("#FFFFFF")]
    [InlineData("#000000")]
    [InlineData("#808080")]
    [InlineData("#00FF7F")]
    public void Every_tint_reaches_aa_contrast_on_white(string color) => AssertReadable(color);

    [Fact]
    public void Every_type_colour_reaches_aa_contrast_on_white()
    {
        foreach (var color in Tint.TypeColors.Values)
        {
            AssertReadable(color);
        }
    }

    [Theory]
    [InlineData("#FFFF00", "#737300")]
    [InlineData("#FF4500", "#d93b00")]
    [InlineData("#FFFFFF", "#737373")]
    [InlineData("#0000FF", "#0000ff")] // already dark enough: kept
    [InlineData("#000000", "#000000")]
    public void A_bar_fill_is_darkened_until_white_text_reads_on_it(string color, string expected)
    {
        Assert.Equal(expected, Tint.Darkened(color));
        Assert.True(Tint.Contrast(Tint.Darkened(color), "#ffffff") >= Tint.MinContrast);
    }

    [Fact]
    public void A_name_has_the_colour_of_its_bar_in_both_themes()
    {
        // User decision „die Balken-Farbe zum Standard“ (feat/damage-text-white): one colour, no dark-theme variant.
        foreach (var color in new[] { "#FFFF00", "#FF4500", "#0000FF", "#8A2BE2", "#00FF7F", "#FFFFFF" }.Concat(Tint.TypeColors.Values))
        {
            Assert.Equal($"--tint: {Tint.Darkened(color)}", Tint.Style(color));
        }
    }

    [Theory]
    [InlineData("#1A2b3C", true)]
    [InlineData(null, false)]
    [InlineData("red", false)]
    [InlineData("#12345G", false)]
    [InlineData("#fff;x", false)]
    public void Only_hex_colours_count_as_colours(string? value, bool expected) => Assert.Equal(expected, Tint.IsColor(value));

    [Fact]
    public void A_colour_that_already_passes_stays_as_picked() =>
        Assert.Equal("--tint: #0000ff", Tint.Style("#0000FF"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#12345G")]
    [InlineData("#-12345")]
    public void Anything_but_a_hex_colour_gives_no_tint(string? color) => Assert.Null(Tint.Style(color));

    [Fact]
    public void The_type_colours_match_the_type_chips_in_site_css()
    {
        var css = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "PokeArena.Web", "wwwroot", "css", "site.css"));
        var chips = TypeRule().Matches(css).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);

        Assert.Equal(chips.OrderBy(c => c.Key), Tint.TypeColors.OrderBy(c => c.Key));
        // The light theme's surface comes first; the dark one follows under prefers-color-scheme.
        Assert.Equal(Tint.LightSurface, Regex.Match(css, @"--surface: (#[0-9a-f]{6});").Groups[1].Value);
        // A damage box side of several colours takes the HP bar's green (review feat/damage-meter).
        Assert.Equal(DamageMeter.Mixed, Regex.Match(css, @"--hp-fill: (#[0-9a-f]{6});").Groups[1].Value);
    }

    [Fact]
    public void The_critical_hit_text_is_readable_on_its_gold_background()
    {
        var css = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "PokeArena.Web", "wwwroot", "css", "site.css"));
        var rule = Regex.Match(css, @"\.log \.critical \{[^}]*background: (#[0-9a-f]{6});[^}]*color: (#[0-9a-f]{6});");

        Assert.True(rule.Success, ".log .critical with background and color not found in site.css");
        Assert.True(Tint.Contrast(rule.Groups[2].Value, rule.Groups[1].Value) >= Tint.MinContrast);
    }

    private static void AssertReadable(string color)
    {
        var tint = Regex.Match(Tint.Style(color)!, "--tint: (#[0-9a-f]{6})").Groups[1].Value;
        Assert.True(Tint.Contrast(tint, Tint.LightSurface) >= Tint.MinContrast, $"{color} → {tint} on {Tint.LightSurface}");
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PokeArena.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("PokeArena.slnx not found above the test output");
    }

    [GeneratedRegex(@"\.(type-[a-z]+) \{ background: (#[0-9a-f]{6}); \}")]
    private static partial Regex TypeRule();
}
