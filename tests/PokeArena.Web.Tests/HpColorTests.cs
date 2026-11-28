using System.Text.RegularExpressions;
using PokeArena.Web.Services;

namespace PokeArena.Web.Tests;

public class HpColorTests
{
    private const int Max = 1000;

    [Theory]
    [InlineData(0, "#c42d0b", "#ff8a6e")] // red only at 0
    [InlineData(500, "#8a6d00", "#e2c044")] // yellow at half: a dark ochre under white numbers, bright on the dark page
    [InlineData(1000, "#1b7f45", "#4cc283")] // green only when full
    public void The_anchors_are_red_yellow_and_green(int hp, string fill, string onDark) =>
        Assert.Equal((fill, onDark), (HpColor.Fill(hp, Max), HpColor.OnDark(hp, Max)));

    [Fact]
    public void Only_none_left_is_red_and_only_full_is_green()
    {
        // The whole range moves (user decision 2026-11-28, claude/health-bar-gradient-166dce): before, a quarter and
        // less was one red and three quarters and more one green, and viewers saw a few fixed colours.
        Assert.NotEqual((HpColor.Fill(0, Max), HpColor.OnDark(0, Max)), (HpColor.Fill(1, Max), HpColor.OnDark(1, Max)));
        Assert.NotEqual((HpColor.Fill(Max, Max), HpColor.OnDark(Max, Max)), (HpColor.Fill(Max - 1, Max), HpColor.OnDark(Max - 1, Max)));
        Assert.NotEqual(HpColor.Fill(0, Max), HpColor.Fill(250, Max));
        Assert.NotEqual(HpColor.Fill(Max, Max), HpColor.Fill(750, Max));
    }

    [Fact]
    public void Every_hp_gets_its_own_shade()
    {
        // Gradual, not steps: from none to full the colour keeps moving, in small steps.
        foreach (var colour in new Func<int, int, string>[] { HpColor.Fill, HpColor.OnDark })
        {
            var shades = Enumerable.Range(0, Max + 1).Select(hp => colour(hp, Max)).ToList();

            Assert.True(shades.Distinct().Count() > 300, $"only {shades.Distinct().Count()} shades");
            Assert.All(shades.Zip(shades.Skip(1)), pair => Assert.True(Distance(pair.First, pair.Second) <= 6, $"{pair.First} → {pair.Second}"));
        }
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

    [Fact]
    public void The_anchors_match_the_colours_site_css_shares_with_them()
    {
        // The green is also the bars' fallback and Bar.Mixed („the HP bar's green“ of the „Schaden“ and statistics bars),
        // the red also the damage „−20“ and the text view's damage: changing an anchor only here would split them (review).
        Assert.Equal([HpColor.Fill(Max, Max)], Values("--hp-fill"));
        Assert.Equal(Bar.Mixed, HpColor.Fill(Max, Max));
        Assert.Equal([HpColor.Fill(0, Max), HpColor.OnDark(0, Max)], Values("--log-hp-low"));
        Assert.Equal([HpColor.Fill(0, Max), HpColor.OnDark(0, Max)], Values("--damage"));
    }

    private static int Distance(string a, string b) =>
        Enumerable.Range(0, 3).Max(i => Math.Abs(Convert.ToInt32(a.Substring(1 + (2 * i), 2), 16) - Convert.ToInt32(b.Substring(1 + (2 * i), 2), 16)));

    /// <summary>The light and the dark theme's <c>--surface</c> from site.css, where the log sits.</summary>
    private static (string Light, string Dark) Surfaces()
    {
        var surfaces = Values("--surface");
        return (surfaces[0], surfaces[1]);
    }

    /// <summary>Every value site.css gives <paramref name="property"/>, in order: the light theme's first, then the dark one's.</summary>
    private static List<string> Values(string property)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "PokeArena.slnx")))
        {
            root = root.Parent ?? throw new DirectoryNotFoundException("PokeArena.slnx not found above the test output");
        }

        var css = File.ReadAllText(Path.Combine(root.FullName, "src", "PokeArena.Web", "wwwroot", "css", "site.css"));
        return Regex.Matches(css, Regex.Escape(property) + @": (#[0-9a-f]{6});").Select(m => m.Groups[1].Value).ToList();
    }
}
