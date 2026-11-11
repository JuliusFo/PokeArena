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
    public void The_leaderboard_shows_small_pictures_and_names_in_their_chat_colour()
    {
        Put(
            SitePaths.Leaderboard,
            new Leaderboard(Now, 3, [
                new(1, "11", "Anna", 3, 4, "#1E90FF", "profilbild/11.png"),
                new(2, "12", "Ben", 1, 1, "red; background: url(x)", "https://evil.example/profilbild/12.png"),
                new(3, "13", "Cleo", 1, 1)]),
            SiteJson.Default.Leaderboard);

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("tbody tr").Count));
        Assert.Equal(
            ["https://daten.example/profilbild/11.png", null, null],
            cut.FindAll("tbody .trainer-link").Select(a => a.QuerySelector("img")?.GetAttribute("src")));
        Assert.Equal(2, cut.FindAll("tbody span.profile-image").Count);
        Assert.Equal("lazy", cut.Find("tbody img").GetAttribute("loading"));
        Assert.Equal(
            [("Anna", "trainer-link tinted", Tint.Style("#1E90FF")), ("Ben", "trainer-link", null), ("Cleo", "trainer-link", null)],
            cut.FindAll("tbody .trainer-link").Select(a => (a.TextContent.Trim(), a.GetAttribute("class"), a.GetAttribute("style"))));
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
                    new BattleSide("Trainer", true, [new BattleFighter(Bisasam, "Ash", "11", 40, HpLeft: 8, Used: true), new BattleFighter(Raupy, "Ash", "11", 30, HpLeft: 30, Used: false)]),
                    new BattleSide("Misty", false, [new BattleFighter(Raupy, "Misty", "12", 30, HpLeft: 0, Used: true)]),
                ],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 5));

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".fighters li").Count));
        var rows = cut.FindAll(".fighters li");
        Assert.Equal(
            [("fighter-survived", "8 / 40 HP"), ("fighter-unused", "nicht eingesetzt"), ("fighter-fainted", "besiegt")],
            rows.Select(li => (li.ClassName,
                (li.QuerySelector(".end-state .hp-text:not(.on-fill)") ?? li.QuerySelector(".end-state")!).TextContent.Trim())));
        // 8 of 40 HP is a fifth: the bar is short and marked low; the numbers sit inside it (feat/hp-text-in-bar).
        // Two copies of the numbers, the white one over the fill hidden from screen readers (fix/hp-text-crisp).
        var bar = rows[0].QuerySelector(".end-state > .hp-bar")!;
        Assert.Equal(("hp-bar low", "--hp: 20%"), (bar.ClassName, bar.GetAttribute("style")));
        Assert.Equal(
            [("hp-text", "8 / 40 HP", null), ("hp-text on-fill", "8 / 40 HP", "true")],
            bar.Children.Select(e => (e.ClassName, e.TextContent, e.GetAttribute("aria-hidden"))));
        // The owner links keep the link colour: no „muted“ on the link itself (review feat/battle-end-state).
        var links = cut.FindAll(".fighters a");
        Assert.Equal(["trainer/11", "trainer/11"], links.Select(a => a.GetAttribute("href")));
        Assert.All(links, a => Assert.False(a.ClassList.Contains("muted")));
        // Species and owner wrap in their own box; anything else straight in the row pushes the HP bar onto a line of
        // its own again (fix/fighter-row-wrap).
        Assert.All(rows, li => Assert.Equal(["who", "end-state"], li.Children.Select(c => c.ClassName)));
        Assert.All(links, a => Assert.Equal("who", a.ParentElement!.ClassName));
        // Written before the damage was stored: no „Schaden“ box (feat/damage-meter).
        Assert.Empty(cut.FindAll(".damage"));
    }

    [Fact]
    public void A_battle_shows_who_dealt_how_much_damage_between_the_cards_and_the_log()
    {
        Put(
            SitePaths.Battle(6),
            BattlePage(6) with
            {
                Sides =
                [
                    new BattleSide("Ash", true, [new BattleFighter(Bisasam, "Ash", "11", 40, HpLeft: 8, Used: true, Color: "#FFFF00", Damage: 30)]),
                    new BattleSide("Misty", false, [new BattleFighter(Raupy, "Misty", "12", 30, HpLeft: 0, Used: true, Color: "#0000FF", Damage: 32)]),
                ],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 6));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".damage")));
        var box = cut.Find(".damage");
        Assert.Equal("sides", box.PreviousElementSibling!.ClassName);
        Assert.Equal("Kampfverlauf", box.NextElementSibling!.TextContent);
        Assert.Equal(["Schaden", "Gesamt", "Pokémon"], box.QuerySelectorAll("h2, h3").Select(h => h.TextContent));
        var ranking = box.QuerySelectorAll("ol > li");
        Assert.Equal(["1.", "2."], ranking.Select(li => li.QuerySelector(".rank")!.TextContent));
        // Text twice, each copy clipped to its part of the bar; the copy over the fill is hidden from screen readers.
        var bar = ranking[0].QuerySelector(".damage-bar")!;
        Assert.Equal("--share: 100%; --fill: #0000ff", bar.GetAttribute("style"));
        // Only a bar that stops short gets the dark edge at its end (review feat/damage-meter).
        Assert.Equal(["damage-bar full", "damage-bar"], ranking.Select(li => li.QuerySelector(".damage-bar")!.ClassName));
        Assert.Equal(
            [("damage-text", "Raupy Misty", "32", null), ("damage-text on-fill", "Raupy Misty", "32", "true")],
            bar.Children.Select(e => (e.ClassName, e.QuerySelector(".who")!.TextContent, e.QuerySelector(".value")!.TextContent, e.GetAttribute("aria-hidden"))));
        // Fills are darkened like the names, so the white text reads (feat/damage-text-white).
        Assert.Equal("--share: 93.8%; --fill: #737300", ranking[1].QuerySelector(".damage-bar")!.GetAttribute("style"));
    }

    [Fact]
    public void The_log_shows_trainers_in_their_chat_colour_and_the_boss_in_its_type_colour()
    {
        var arkani = new SpeciesInfo(59, "Arkani", ["Feuer"]);
        Put(
            SitePaths.Battle(4),
            BattlePage(4) with
            {
                Kind = BattleKind.Boss,
                Sides =
                [
                    new BattleSide("Trainer", true, [new BattleFighter(Bisasam, "Ash", "11", 40, Color: "#FFFF00"), new BattleFighter(Raupy, "Misty", "12", 30)]),
                    new BattleSide("Arkani", false, [new BattleFighter(arkani, "Boss", null, 400)]),
                ],
                Lines = ["Runde 1", "Bisasam (Ash) hat Arkani (Boss) 20 Schaden zugefügt. Volltreffer! Sehr effektiv!", "Raupy (Misty) wurde besiegt."],
                Log =
                [
                    new LogLine([new LogPart("Runde 1")], IsRound: true),
                    new LogLine(
                    [
                        new LogPart("Bisasam", 0, 0, IsSpecies: true), new LogPart(" (", 0, 0), new LogPart("Ash", 0, 0, IsOwner: true), new LogPart(")", 0, 0), new LogPart(" hat "),
                        new LogPart("Arkani", 1, 0, IsSpecies: true), new LogPart(" (", 1, 0), new LogPart("Boss", 1, 0, IsOwner: true), new LogPart(")", 1, 0), new LogPart(" 20 Schaden zugefügt."),
                        new LogPart(" Volltreffer!", IsCritical: true), new LogPart(" Sehr effektiv!"),
                    ]),
                    new LogLine(
                    [
                        new LogPart("Raupy (", 0, 1), new LogPart("Misty", 0, 1, IsOwner: true), new LogPart(")", 0, 1), new LogPart(" wurde besiegt."),
                        new LogPart("?", 5, 0, IsOwner: true),
                    ]),
                ],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 4));

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".log li").Count));
        var lines = cut.FindAll(".log li");
        Assert.Equal(["Runde 1", "Bisasam (Ash) hat Arkani (Boss) 20 Schaden zugefügt. Volltreffer! Sehr effektiv!", "Raupy (Misty) wurde besiegt.?"], lines.Select(li => li.TextContent));
        Assert.Equal("round", lines[0].ClassName);
        // A trainer's name and the boss's species only, „Volltreffer!“ highlighted below; Misty has no colour; an index the page lacks stays plain text.
        var fire = Tint.Style(Tint.TypeColors["type-feuer"]);
        Assert.Equal(
            [("Ash", Tint.Style("#FFFF00")), ("Arkani", fire)],
            cut.FindAll(".log .tinted").Select(s => (s.TextContent, s.GetAttribute("style"))));
        // „Volltreffer!“ bold on gold; its leading space stays outside the highlight.
        var critical = cut.Find(".log .critical");
        Assert.Equal(("STRONG", "Volltreffer!"), (critical.TagName, critical.TextContent));
        Assert.Equal(["tinted", null], cut.FindAll(".fighters a").Select(a => a.GetAttribute("class")));
    }

    [Fact]
    public void A_deleted_trainer_in_a_boss_fight_is_not_coloured_like_the_boss()
    {
        var arkani = new SpeciesInfo(59, "Arkani", ["Feuer"]);
        Put(
            SitePaths.Battle(3),
            BattlePage(3) with
            {
                Kind = BattleKind.Boss,
                Sides =
                [
                    new BattleSide("Trainer", true, [new BattleFighter(Raupy, "[gelöscht]", null, 30)]),
                    new BattleSide("Arkani", false, [new BattleFighter(arkani, "Boss", null, 400)]),
                ],
                Lines = ["Raupy ([gelöscht]) hat Arkani (Boss) 5 Schaden zugefügt."],
                Log =
                [
                    new LogLine(
                    [
                        new LogPart("Raupy", 0, 0, IsSpecies: true), new LogPart(" (", 0, 0), new LogPart("[gelöscht]", 0, 0, IsOwner: true), new LogPart(")", 0, 0), new LogPart(" hat "),
                        new LogPart("Arkani", 1, 0, IsSpecies: true), new LogPart(" (", 1, 0), new LogPart("Boss", 1, 0, IsOwner: true), new LogPart(")", 1, 0), new LogPart(" 5 Schaden zugefügt."),
                    ]),
                ],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 3));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".log li")));
        Assert.Equal(["Arkani"], cut.FindAll(".log .tinted").Select(s => s.TextContent));
    }

    [Fact]
    public void A_critical_hit_part_without_text_renders_as_plain_text()
    {
        _host.Files[SitePaths.Battle(7)] = """
            {"id":7,"kind":"Pvp","foughtAt":"2026-11-01T17:30:00+00:00","seed":1,"headline":"Ash gegen Misty","result":"Ash besiegt Misty.",
            "sides":[],"lines":["5 Schaden zugefügt. "],
            "log":[{"parts":[{"text":"5 Schaden zugefügt."},{"isCritical":true},{"text":" ","isCritical":true}]}]}
            """;

        var cut = Render<Battle>(p => p.Add(b => b.Id, 7));

        cut.WaitForAssertion(() => Assert.Equal("5 Schaden zugefügt. ", cut.Find(".log li").TextContent));
        Assert.Empty(cut.FindAll(".log .critical"));
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

    [Theory]
    [InlineData("profilbild/11.png", "https://daten.example/profilbild/11.png")]
    [InlineData("profilbild/11.jpg", "https://daten.example/profilbild/11.jpg")]
    [InlineData(null, null)]
    [InlineData("https://evil.example/profilbild/11.png", null)]
    [InlineData("//evil.example/profilbild/11.png", null)]
    [InlineData("profilbild/../trainer/11.png", null)]
    public void The_profile_picture_comes_only_from_the_data_host(string? path, string? expected)
    {
        Put(
            SitePaths.Trainer("11"),
            new TrainerPage("11", "Anna", Now, Now, 1, 1, 0, 0, 0, [new(Bisasam, 1, true)], [], [], [], path),
            SiteJson.Default.TrainerPage);

        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));

        cut.WaitForAssertion(() => Assert.Contains("Anna", cut.Find(".trainer-head h1").TextContent));
        Assert.Equal(expected, cut.FindAll(".trainer-head img").SingleOrDefault()?.GetAttribute("src"));
    }

    [Theory]
    [InlineData("#1E90FF", "tinted", "--tint: #1873cc")]
    [InlineData(null, null, null)]
    [InlineData("blue", null, null)]
    public void The_trainer_name_has_the_chat_colour(string? color, string? expectedClass, string? expectedStyle)
    {
        Put(
            SitePaths.Trainer("11"),
            new TrainerPage("11", "Anna", Now, Now, 1, 1, 0, 0, 0, [new(Bisasam, 1, true)], [], [], [], Color: color),
            SiteJson.Default.TrainerPage);

        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));

        cut.WaitForAssertion(() => Assert.Contains("Anna", cut.Find(".trainer-head h1").TextContent));
        var heading = cut.Find(".trainer-head h1");
        Assert.Equal((expectedClass, expectedStyle), (heading.GetAttribute("class"), heading.GetAttribute("style")));
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
    public void The_menu_links_to_the_statistics_the_guide_and_the_news()
    {
        var cut = Render<MainLayout>(p => p.Add(l => l.Body, b => b.AddContent(0, "Inhalt")));

        var links = cut.FindAll(".site-header nav a").Select(a => a.GetAttribute("href")).ToList();
        Assert.Contains("statistik", links);
        Assert.Contains("anleitung", links);
        Assert.Contains("neuigkeiten", links);
    }

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

    [Fact]
    public void The_news_lists_the_newest_version_first_and_dates_every_released_one()
    {
        var cut = Render<News>();

        var headings = cut.FindAll(".news h2").Select(h => h.TextContent.Trim()).ToList();
        Assert.NotEmpty(headings);

        // Only the newest section may still lack its date — /release sets it (WP-10).
        var dated = new Regex(@"^Version (\d+\.\d+\.\d+) – \d{1,2}\. \p{L}+ \d{4}$");
        var undated = new Regex(@"^Version (\d+\.\d+\.\d+)$");
        Assert.Matches(new Regex($"{undated}|{dated}"), headings[0]);
        foreach (var heading in headings.Skip(1))
        {
            Assert.Matches(dated, heading);
        }

        var versions = headings.Select(h => Version.Parse(Regex.Match(h, @"^Version (\d+\.\d+\.\d+)").Groups[1].Value)).ToList();
        Assert.Equal(versions.OrderDescending(), versions);
        Assert.Equal(versions.Count, versions.Distinct().Count());
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
