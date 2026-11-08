using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Contracts;
using PokeArena.Web.Layout;
using PokeArena.Web.Pages;
using PokeArena.Web.Services;

namespace PokeArena.Web.Tests;

public class PageTests : BunitContext
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 17, 30, 0, TimeSpan.Zero);
    private static readonly SpeciesInfo Bisasam = new(1, "Bisasam", ["Pflanze", "Gift"]);
    private static readonly SpeciesInfo Raupy = new(10, "Raupy", ["Käfer"]);

    private readonly FakeDataHost _host = new();
    private readonly FakeTimeProvider _time = new(Now);

    public PageTests()
    {
        Services.AddSingleton<TimeProvider>(_time);
        Services.AddSingleton(new SiteData(new HttpClient(_host) { BaseAddress = new Uri("https://daten.example/") }));
    }

    [Fact]
    public void The_leaderboard_lists_trainers_with_links_to_their_pages()
    {
        Put(SitePaths.Leaderboard, new Leaderboard(Now, 2, [new(1, "11", "Anna", 3, 4), new(2, "12", "Ben", 1, 1)]), SiteJson.Default.Leaderboard);

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tbody tr").Count));
        Assert.Equal("trainer/11", cut.Find("tbody a").GetAttribute("href"));
        Assert.Contains("2 Trainer · Stand 18:30 Uhr", cut.Markup);
    }

    [Fact]
    public void The_leaderboard_polls_and_explains_an_empty_game()
    {
        var cut = Render<Home>();
        cut.WaitForAssertion(() => Assert.Contains("!registrieren", cut.Markup));

        Put(SitePaths.Leaderboard, new Leaderboard(Now, 1, [new(1, "11", "Anna", 1, 1)]), SiteJson.Default.Leaderboard);
        _time.Advance(PollingPage.Interval);

        cut.WaitForAssertion(() => Assert.Contains("Anna", cut.Markup));
    }

    [Fact]
    public void An_unreachable_data_host_shows_a_hint_and_keeps_trying()
    {
        _host.Down = true;

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains("konnten gerade nicht geladen werden", cut.Markup));
        _host.Down = false;
        Put(SitePaths.Leaderboard, new Leaderboard(Now, 1, [new(1, "11", "Anna", 1, 1)]), SiteJson.Default.Leaderboard);
        _time.Advance(PollingPage.Interval);
        cut.WaitForAssertion(() => Assert.DoesNotContain("konnten gerade nicht geladen werden", cut.Markup));
    }

    [Fact]
    public void A_battle_linked_before_its_upload_appears_once_it_is_there()
    {
        var cut = Render<Battle>(p => p.Add(b => b.Id, 7));
        cut.WaitForAssertion(() => Assert.Contains("Kampf wird gerade veröffentlicht", cut.Markup));

        Put(SitePaths.Battle(7), BattlePage(7), SiteJson.Default.BattlePage);
        _time.Advance(Battle.RetryInterval);

        cut.WaitForAssertion(() => Assert.Contains("Ash gegen Misty", cut.Markup));
        Assert.Contains("Ash – gewonnen", cut.Markup);
        Assert.Equal(["Runde 1", "Bisasam (Ash, HP 40) hat Raupy (Misty, HP 0) 50 Schaden zugefügt."], cut.FindAll(".log li").Select(li => li.TextContent));
        Assert.NotNull(cut.Find(".type-kaefer"));
    }

    [Fact]
    public void A_battle_that_never_appears_is_reported_missing()
    {
        var cut = Render<Battle>(p => p.Add(b => b.Id, 8));
        cut.WaitForAssertion(() => Assert.Contains("Kampf wird gerade veröffentlicht", cut.Markup));

        for (var waited = TimeSpan.Zero; waited <= Battle.GiveUpAfter; waited += Battle.RetryInterval)
        {
            _time.Advance(Battle.RetryInterval);
            cut.WaitForState(() => true);
        }

        cut.WaitForAssertion(() => Assert.Contains("Kampf nicht gefunden", cut.Markup));
    }

    [Fact]
    public void A_trainer_can_be_opened_by_name()
    {
        Put(SitePaths.Leaderboard, new Leaderboard(Now, 1, [new(4, "11", "Anna", 1, 2)]), SiteJson.Default.Leaderboard);
        Put(
            SitePaths.Trainer("11"),
            new TrainerPage("11", "Anna", Now, Now, 1, 2, 3, 5, 4, [new(Bisasam, 2, true)], [new(15, "Eich", Now)], [new("Eich", Now, 9)], []),
            SiteJson.Default.TrainerPage);

        var cut = Render<Trainer>(p => p.Add(t => t.Key, "ANNA"));

        cut.WaitForAssertion(() => Assert.Contains("Platz 4", cut.Markup));
        Assert.Contains("#001", cut.Markup);
        Assert.Contains("× 2", cut.Markup);
        Assert.Contains("Orden von Eich", cut.Markup);
        Assert.Equal("kampf/9", cut.Find(".chip-badge a").GetAttribute("href"));
    }

    [Fact]
    public void An_unknown_trainer_is_reported()
    {
        var cut = Render<Trainer>(p => p.Add(t => t.Key, "999"));

        cut.WaitForAssertion(() => Assert.Contains("Trainer nicht gefunden", cut.Markup));
    }

    [Fact]
    public void The_guide_explains_the_commands_and_lists_every_npc()
    {
        var cut = Render<Guide>();

        Assert.Contains("!registrieren", cut.Markup);
        Assert.Contains("!challenge", cut.Markup);
        var npcs = cut.FindAll(".guide-npcs tbody tr");
        Assert.Equal(17, npcs.Count);
        Assert.Equal("15Eich", npcs[0].TextContent);
        Assert.Equal("151Ash", npcs[^1].TextContent);
    }

    [Fact]
    public void The_menu_links_to_the_guide()
    {
        var cut = Render<MainLayout>(p => p.Add(l => l.Body, b => b.AddContent(0, "Inhalt")));

        Assert.Contains(cut.FindAll(".site-header nav a"), a => a.GetAttribute("href") == "anleitung");
    }

    [Theory]
    [InlineData("Käfer", "type-kaefer")]
    [InlineData("Psycho", "type-psycho")]
    public void Type_names_become_css_classes(string type, string css) => Assert.Equal(css, Format.TypeClass(type));

    private static BattlePage BattlePage(long id) => new(
        id,
        BattleKind.Pvp,
        Now,
        42,
        "Ash gegen Misty",
        "Ash besiegt Misty und bekommt Raupy von Misty!",
        [
            new BattleSide("Ash", true, [new BattleFighter(Bisasam, "Ash", "11", 40)]),
            new BattleSide("Misty", false, [new BattleFighter(Raupy, "Misty", "12", 30)]),
        ],
        ["Runde 1", "Bisasam (Ash, HP 40) hat Raupy (Misty, HP 0) 50 Schaden zugefügt."]);

    private void Put<T>(string path, T value, JsonTypeInfo<T> type) => _host.Files[path] = JsonSerializer.Serialize(value, type);
}
