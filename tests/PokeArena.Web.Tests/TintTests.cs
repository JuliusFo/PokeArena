using System.Text.RegularExpressions;
using PokeArena.Web.Services;

namespace PokeArena.Web.Tests;

public partial class TintTests
{
    [Theory]
    [InlineData("#FFFF00")] // yellow: unreadable on white
    [InlineData("#0000FF")] // blue: unreadable on the dark surface
    [InlineData("#FFFFFF")]
    [InlineData("#000000")]
    [InlineData("#808080")]
    [InlineData("#00FF7F")]
    public void Every_tint_reaches_aa_contrast_in_both_themes(string color) => AssertReadable(color);

    [Fact]
    public void Every_type_colour_reaches_aa_contrast_in_both_themes()
    {
        foreach (var color in Tint.TypeColors.Values)
        {
            AssertReadable(color);
        }
    }

    [Fact]
    public void A_colour_that_already_passes_stays_as_picked() =>
        Assert.Equal("--tint-light: #0000ff; --tint-dark: #8080ff", Tint.Style("#0000FF"));

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
        // Light theme first, then the dark one under prefers-color-scheme.
        Assert.Equal([Tint.LightSurface, Tint.DarkSurface], Regex.Matches(css, @"--surface: (#[0-9a-f]{6});").Select(m => m.Groups[1].Value));
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
        var style = Tint.Style(color)!;
        var light = Regex.Match(style, "--tint-light: (#[0-9a-f]{6})").Groups[1].Value;
        var dark = Regex.Match(style, "--tint-dark: (#[0-9a-f]{6})").Groups[1].Value;
        Assert.True(Tint.Contrast(light, Tint.LightSurface) >= Tint.MinContrast, $"{color} → {light} on {Tint.LightSurface}");
        Assert.True(Tint.Contrast(dark, Tint.DarkSurface) >= Tint.MinContrast, $"{color} → {dark} on {Tint.DarkSurface}");
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
