using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Contracts;
using PokeArena.Web.Layout;
using PokeArena.Web.Shared;
using PokeArena.Web.Pages;
using PokeArena.Web.Services;

namespace PokeArena.Web.Tests;

/// <summary>The „Statistik“ page.</summary>
public class StatsTests : WebTestContext
{
    [Fact]
    public void The_statistics_show_each_list_as_bars_with_links_to_the_trainers()
    {
        var anna = new TrainerRef("11", "Anna", "#FF69B4");
        var ben = new TrainerRef("12", "Ben");
        Put(SitePaths.Statistics, new Statistics(
            Now,
            new CommunityTotals(38, 151, 2, 27, 31),
            [new TrainerCount(1, anna, 14), new TrainerCount(2, ben, 11)],
            [new PvpRecord(1, ben, 9, 1)],
            [new BadLuckStreak(1, anna, 5, true)],
            12,
            [new SingleOwned(Bisasam, anna), new SingleOwned(Raupy, ben)],
            [new Rivalry(1, ben, anna, 5, 3, 2)],
            [new BossTally(1, Raupy, 4)],
            []), SiteJson.Default.Statistics);

        var cut = Render<Stats>();

        cut.WaitForAssertion(() => Assert.Contains("Gemeinsamer Pokédex", cut.Markup));
        Assert.Equal(
            ["Arten 38 / 151", "Anna 14", "Ben 11", "Anna (läuft noch) 5", "Ben 9 Kämpfe · 1 Sieg", "Ben – Anna 5 Kämpfe · 3 : 2", "Raupy 4"],
            cut.FindAll(".damage-text:not(.on-fill)").Select(t => Regex.Replace(t.TextContent, @"\s+", " ").Trim()));
        Assert.Equal(["1.", "2."], cut.FindAll(".bar-box")[1].QuerySelectorAll(".rank").Select(r => r.TextContent));
        Assert.Equal("trainer/11", cut.Find(".damage-text:not(.on-fill) a").GetAttribute("href"));
        Assert.Equal("-1", cut.Find(".damage-text.on-fill a").GetAttribute("tabindex"));
        Assert.Contains("12 Arten hat nur ein einziger Trainer. Hier die seltensten:", Regex.Replace(cut.Markup, @"\s+", " "));
        Assert.Equal(["Anna", "Ben"], cut.FindAll(".singles a").Select(a => a.TextContent));
        Assert.Contains("Bisher wurde jeder Boss gefangen.", cut.Markup);
    }

    [Fact]
    public void The_statistics_explain_a_missing_file_an_empty_game_and_lists_without_rows()
    {
        // Right after the release the site is live before the bot has written the file — not an empty game.
        var cut = Render<Stats>();
        cut.WaitForAssertion(() => Assert.Contains("wird gerade erstellt und erscheint bald", cut.Markup));
        Assert.DoesNotContain("!registrieren", cut.Markup);

        Put(SitePaths.Statistics, new Statistics(Now, new CommunityTotals(0, 151, 0, 0, 0), [], [], [], 0, [], [], [], []), SiteJson.Default.Statistics);
        _time.Advance(PollingPage.Interval);
        cut.WaitForAssertion(() => Assert.Contains("!registrieren", cut.Markup));

        Put(SitePaths.Statistics, new Statistics(Now, new CommunityTotals(1, 151, 1, 0, 0), [], [], [], 0, [], [], [], []), SiteJson.Default.Statistics);
        _time.Advance(PollingPage.Interval);

        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll(".bar-box p.muted").Count(p => p.TextContent == "Noch niemand.")));
        Assert.Contains("Noch kein Boss-Kampf.", cut.Markup);
        Assert.Contains("keine Art, die nur ein Trainer hat", cut.Markup);
    }
}
