using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Contracts;
using PokeArena.Web.Layout;
using PokeArena.Web.Shared;
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
    public void A_timeout_counts_as_a_failed_load_and_polling_goes_on()
    {
        _host.TimesOut = true;

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains("konnten gerade nicht geladen werden", cut.Markup));
        Assert.DoesNotContain("Noch niemand dabei", cut.Markup);
        _host.TimesOut = false;
        Put(SitePaths.Leaderboard, new Leaderboard(Now.AddDays(-2), 1, [new(1, "11", "Anna", 1, 1)]), SiteJson.Default.Leaderboard);
        _time.Advance(PollingPage.Interval);
        cut.WaitForAssertion(() => Assert.Contains("Anna", cut.Markup));
        Assert.Contains("Stand 30.10.2026, 18:30 Uhr", cut.Markup);
    }

    [Fact]
    public void A_battle_that_cannot_be_loaded_is_not_reported_missing()
    {
        _host.Down = true;
        var cut = Render<Battle>(p => p.Add(b => b.Id, 8));

        for (var waited = TimeSpan.Zero; waited <= Battle.GiveUpAfter; waited += Battle.RetryInterval)
        {
            _time.Advance(Battle.RetryInterval);
            cut.WaitForState(() => true);
        }

        cut.WaitForAssertion(() => Assert.Contains("Bitte lade die Seite gleich noch einmal", cut.Markup));
        Assert.DoesNotContain("nicht gefunden", cut.Markup);
    }

    [Fact]
    public void Data_in_an_older_format_gets_a_hint()
    {
        Put(SitePaths.Meta, new SiteMeta(SiteMeta.CurrentFormatVersion - 1, Now), SiteJson.Default.SiteMeta);

        var cut = Render<MainLayout>(p => p.Add(l => l.Body, b => b.AddContent(0, "Inhalt")));

        cut.WaitForAssertion(() => Assert.Contains("Die Daten werden gerade umgestellt", cut.Markup));
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
    public void A_battle_shows_how_each_pokemon_ended()
    {
        Put(
            SitePaths.Battle(5),
            BattlePage(5) with
            {
                Sides =
                [
                    new BattleSide("Ash", true, [new BattleFighter(Bisasam, "Ash", "11", 40, HpLeft: 8, Used: true), new BattleFighter(Raupy, "Ash", "11", 30, HpLeft: 30, Used: false)]),
                    new BattleSide("Misty", false, [new BattleFighter(Raupy, "Misty", "12", 30, HpLeft: 0, Used: true)]),
                ],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 5));

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".fighters li").Count));
        var rows = cut.FindAll(".fighters li");
        Assert.Equal(
            [("fighter-survived", "8 / 40 HP"), ("fighter-unused", "nicht eingesetzt"), ("fighter-fainted", "besiegt")],
            rows.Select(li => (li.ClassName, li.QuerySelector(".end-state")!.TextContent.Trim())));
        // 8 of 40 HP is a fifth: the bar is short and marked low.
        var bar = rows[0].QuerySelector(".hp-bar > span")!;
        Assert.Equal(("low", "width: 20%"), (bar.ClassName, bar.GetAttribute("style")));
    }

    [Fact]
    public void A_battle_file_from_before_the_end_state_shows_the_start_hp()
    {
        _host.Files[SitePaths.Battle(6)] = """
            {"id":6,"kind":"Pvp","foughtAt":"2026-11-01T17:30:00+00:00","seed":1,"headline":"Ash gegen Misty","result":"Ash besiegt Misty.",
            "sides":[{"name":"Ash","won":true,"fighters":[{"species":{"id":1,"name":"Bisasam","types":["Pflanze","Gift"]},"owner":"Ash","trainerId":"11","maxHp":40}]}],
            "lines":[]}
            """;

        var cut = Render<Battle>(p => p.Add(b => b.Id, 6));

        cut.WaitForAssertion(() => Assert.Equal("40 HP", cut.Find(".fighters .end-state").TextContent.Trim()));
        Assert.Equal("fighter-unknown", cut.Find(".fighters li").ClassName);
        Assert.Empty(cut.FindAll(".hp-bar"));
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
        // Every row against data/npcs.json: threshold and name, in order (review 1.0.3, C16).
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "npcs.json")));
        var expected = file.RootElement.GetProperty("npcs").EnumerateArray()
            .Select(n => $"{n.GetProperty("schwelle").GetInt32()}{n.GetProperty("name").GetString()}");
        Assert.Equal(expected, cut.FindAll(".guide-npcs tbody tr").Select(tr => tr.TextContent));
    }

    [Fact]
    public void The_website_references_only_the_contracts()
    {
        // ADR-0009: EF Core, the Twitch client and the bot's logic must never end up in the browser download.
        var references = typeof(Format).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.Contains("PokeArena.Contracts", references);
        Assert.DoesNotContain(references, r => r is "PokeArena.Core" or "PokeArena.UI" or "PokeArena.Desktop" || r.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
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
