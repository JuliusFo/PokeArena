using System.Text.RegularExpressions;
using PokeArena.Web.Services;

namespace PokeArena.Web.Tests;

public class HpColorTests
{
    private const int Max = 1000;

    [Theory]
    [InlineData(0, "#c42d0b", "#ff8a6e")]
    [InlineData(100, "#c42d0b", "#ff8a6e")] // red up to a quarter
    [InlineData(250, "#c42d0b", "#ff8a6e")]
    [InlineData(500, "#a35200", "#f0a04b")] // the former steps' orange at half
    [InlineData(750, "#1b7f45", "#4cc283")] // green from three quarters (user decision 2026-11-27)
    [InlineData(900, "#1b7f45", "#4cc283")]
    [InlineData(1000, "#1b7f45", "#4cc283")]
    public void The_anchors_keep_the_former_steps_colours(int hp, string fill, string onDark) =>
        Assert.Equal((fill, onDark), (HpColor.Fill(hp, Max), HpColor.OnDark(hp, Max)));

    [Fact]
    public void Between_the_anchors_every_hp_gets_its_own_shade()
    {
        // Gradual, not three steps (claude/hp-bar-color-gradient-ee5d5b): from a quarter to three quarters the colour
        // keeps moving, in small steps.
        var fills = Enumerable.Range(250, 501).Select(hp => HpColor.Fill(hp, Max)).ToList();

        Assert.True(fills.Distinct().Count() > 100, $"only {fills.Distinct().Count()} shades");
        Assert.All(fills.Zip(fills.Skip(1)), pair => Assert.True(Distance(pair.First, pair.Second) <= 6, $"{pair.First} → {pair.Second}"));
    }

    [Fact]
    public void Every_shade_reads_with_white_numbers_and_on_both_surfaces()
    {
        var (light, dark) = Surfaces();

        for (var hp = 0; hp <= Max; hp++)
        {
            var fill = HpColor.Fill(hp, Max);
            var onDark = HpColor.OnDark(hp, Max);
            Assert.True(Tint.Contrast(fill, "#ffffff") >= Tint.MinContrast, $"white on {fill} at {hp}");
            Assert.True(Tint.Contrast(fill, light) >= Tint.MinContrast, $"{fill} on {light} at {hp}");
            Assert.True(Tint.Contrast(onDark, dark) >= Tint.MinContrast, $"{onDark} on {dark} at {hp}");
        }
    }

    [Theory]
    [InlineData(-5, 80, 0.0)]
    [InlineData(90, 80, 1.0)] // more than the maximum: full
    [InlineData(5, 0, 1.0)] // no maximum: like the bar's width
    [InlineData(0, 0, 0.0)]
    [InlineData(1_500_000_000, int.MaxValue, 0.698)] // a file not ours: no wrap into red (review claude/hp-coloring-bar-view-4f3c63)
    public void The_share_stays_between_none_and_all(int hp, int max, double share) =>
        Assert.Equal(share, HpColor.Share(hp, max), precision: 3);

    [Fact]
    public void The_text_style_carries_both_themes()
    {
        Assert.Equal("--log-hp: #1b7f45; --log-hp-dark: #4cc283", HpColor.TextStyle(80, 80));
        Assert.Matches(@"^--log-hp: #[0-9a-f]{6}; --log-hp-dark: #[0-9a-f]{6}$", HpColor.TextStyle(53, 80));
    }

    private static int Distance(string a, string b) =>
        Enumerable.Range(0, 3).Max(i => Math.Abs(Convert.ToInt32(a.Substring(1 + (2 * i), 2), 16) - Convert.ToInt32(b.Substring(1 + (2 * i), 2), 16)));

    /// <summary>The light and the dark theme's <c>--surface</c> from site.css, where the log sits.</summary>
    private static (string Light, string Dark) Surfaces()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "PokeArena.slnx")))
        {
            root = root.Parent ?? throw new DirectoryNotFoundException("PokeArena.slnx not found above the test output");
        }

        var css = File.ReadAllText(Path.Combine(root.FullName, "src", "PokeArena.Web", "wwwroot", "css", "site.css"));
        var surfaces = Regex.Matches(css, @"--surface: (#[0-9a-f]{6});").Select(m => m.Groups[1].Value).ToList();
        return (surfaces[0], surfaces[1]);
    }
}
