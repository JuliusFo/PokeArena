using System.Text.RegularExpressions;
using PokeArena.Web.Services;

namespace PokeArena.Web.Tests;

public partial class TintTests
{
    [Theory]
    [InlineData("#FFFF00", "--tint: #ffff00")] // yellow stays yellow, also on white (user decision, feat/damage-text-white)
    [InlineData("#0000FF", "--tint: #0000ff")] // blue stays blue, also on the dark page
    [InlineData("#1A2b3C", "--tint: #1a2b3c")]
    public void A_tint_is_the_colour_as_picked(string color, string expected) => Assert.Equal(expected, Tint.Style(color));

    [Theory]
    [InlineData("#1A2b3C", true)]
    [InlineData(null, false)]
    [InlineData("red", false)]
    [InlineData("#12345G", false)]
    [InlineData("#fff;x", false)]
    public void Only_hex_colours_count_as_colours(string? value, bool expected) => Assert.Equal(expected, Tint.IsColor(value));

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
