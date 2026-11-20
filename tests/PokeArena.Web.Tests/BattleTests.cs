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

/// <summary>The fight page: loading, cards, damage box and the fight log.</summary>
public class BattleTests : WebTestContext
{
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
    public void A_battle_linked_before_its_upload_appears_once_it_is_there()
    {
        var cut = Render<Battle>(p => p.Add(b => b.Id, 7));
        cut.WaitForAssertion(() => Assert.Contains("Kampf wird gerade veröffentlicht", cut.Markup));

        Put(SitePaths.Battle(7), BattlePage(7), SiteJson.Default.BattlePage);
        _time.Advance(Battle.RetryInterval);

        cut.WaitForAssertion(() => Assert.Contains("Ash gegen Misty", cut.Markup));
        Assert.Contains("Ash – gewonnen", cut.Markup);
        Assert.Equal(["Runde 1", "Bisasam (Ash, HP 40) hat Raupy (Misty, HP 45 → 0) 50 Schaden zugefügt."], cut.FindAll(".log li").Select(li => li.TextContent));
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
    public void A_hit_shows_the_targets_hp_as_a_bar_instead_of_in_the_sentence()
    {
        // The first hit leaves 22 of 80, the second is more than the 22 left (the bar loses 22, the text keeps 31), the
        // third took nothing (no minimum-width lost piece then).
        Put(
            SitePaths.Battle(8),
            BattlePage(8) with
            {
                Sides =
                [
                    new BattleSide("Ash", true, [new BattleFighter(Bisasam, "Ash", "11", 90, Color: "#FFFF00")]),
                    new BattleSide("Misty", false, [new BattleFighter(Raupy, "Misty", "12", 80)]),
                ],
                Lines =
                [
                    "Bisasam (Ash, HP 90) hat Raupy (Misty, HP 80 → 22) 58 Schaden zugefügt. Sehr effektiv!",
                    "Bisasam (Ash, HP 90) hat Raupy (Misty, HP 22 → 0) 31 Schaden zugefügt.",
                    "Bisasam (Ash, HP 90) hat Raupy (Misty, HP 80 → 80) 0 Schaden zugefügt.",
                ],
                Log = [Hit(80, 22, " 58 Schaden zugefügt. Sehr effektiv!"), Hit(22, 0, " 31 Schaden zugefügt."), Hit(80, 80, " 0 Schaden zugefügt.")],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 8));

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".log li").Count));
        Assert.Equal(
            [
                "Bisasam (Ash, HP 90) hat Raupy (Misty) 58 Schaden zugefügt. Sehr effektiv!",
                "Bisasam (Ash, HP 90) hat Raupy (Misty) 31 Schaden zugefügt.",
                "Bisasam (Ash, HP 90) hat Raupy (Misty) 0 Schaden zugefügt.",
            ],
            cut.FindAll(".log .hit-text").Select(t => t.TextContent));
        var bars = cut.FindAll(".log .hit-bar");
        Assert.Equal(
            [
                ("hp-bar hit-bar", "--hp: 27.5%; --before: 100%", "HP 80 → 22 von 80", "HP 80 → 22"),
                ("hp-bar hit-bar low", "--hp: 0%; --before: 27.5%", "HP 22 → 0 von 80", "HP 22 → 0"),
                ("hp-bar hit-bar no-loss", "--hp: 100%; --before: 100%", "HP 80 → 80 von 80", "HP 80 → 80"),
            ],
            bars.Select(b => (b.ClassName, b.GetAttribute("style"), b.GetAttribute("title"), b.QuerySelector(".hp-text")!.TextContent)));
        // „HP“ only for screen readers and a copied line; the bar's copy for the fill stays hidden from them, like on the fight cards.
        Assert.All(bars, b => Assert.Equal("HP ", b.QuerySelector(".hp-text .visually-hidden")!.TextContent));
        Assert.All(bars, b => Assert.Equal("true", b.QuerySelector(".hp-text.on-fill")!.GetAttribute("aria-hidden")));
        // A 1.8.1 file has no damage on its hits: the number stays in the sentence, none next to the bar.
        Assert.Empty(cut.FindAll(".log .hit-damage"));

        static LogLine Hit(int before, int after, string outcome) => new(
            [
                new LogPart("Bisasam", 0, 0, IsSpecies: true), new LogPart(" (", 0, 0), new LogPart("Ash", 0, 0, IsOwner: true), new LogPart(", HP 90)", 0, 0), new LogPart(" hat "),
                new LogPart("Raupy", 1, 0, IsSpecies: true), new LogPart(" (", 1, 0), new LogPart("Misty", 1, 0, IsOwner: true),
                new LogPart($", HP {before} → {after}", 1, 0, IsTargetHp: true), new LogPart(")", 1, 0), new LogPart(outcome),
            ],
            Hit: new LogHit(1, 0, before, after));
    }

    [Fact]
    public void A_hit_with_its_damage_shows_the_number_next_to_the_bar_and_a_short_sentence()
    {
        // The second hit is more than the 22 left: the number says what arrived (−22), like the bar and the „Schaden“
        // box — not the 31 of the sentence (review claude/kampflog-hp-balken-feedback-808390).
        Put(
            SitePaths.Battle(10),
            BattlePage(10) with
            {
                Sides =
                [
                    new BattleSide("Ash", true, [new BattleFighter(Bisasam, "Ash", "11", 90)]),
                    new BattleSide("Misty", false, [new BattleFighter(Raupy, "Misty", "12", 80)]),
                ],
                Lines =
                [
                    "Bisasam (Ash, HP 90) hat Raupy (Misty, HP 80 → 22) 58 Schaden zugefügt. Volltreffer! Sehr effektiv!",
                    "Bisasam (Ash, HP 90) hat Raupy (Misty, HP 22 → 0) 31 Schaden zugefügt.",
                ],
                Log =
                [
                    Hit(80, 22, 58, new LogPart(" Volltreffer!", IsCritical: true), new LogPart(" Sehr effektiv!")),
                    Hit(22, 0, 31),
                ],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 10));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".log .hit-damage").Count));
        Assert.Equal(
            ["Bisasam (Ash, HP 90) trifft Raupy (Misty). Volltreffer! Sehr effektiv!", "Bisasam (Ash, HP 90) trifft Raupy (Misty)."],
            cut.FindAll(".log .hit-text").Select(t => t.TextContent));
        Assert.Equal("Volltreffer!", cut.Find(".log .critical").TextContent);
        // The number left of the bar, inside one box with it; „Schaden“ only for screen readers and a copied line.
        Assert.Equal([" −58 Schaden, ", " −22 Schaden, "], cut.FindAll(".log .hit-damage").Select(d => d.TextContent));
        Assert.All(cut.FindAll(".log .hit-damage .visually-hidden"), h => Assert.NotEqual("", h.TextContent));
        Assert.Equal(4, cut.FindAll(".log .hit-damage .visually-hidden").Count);
        Assert.All(cut.FindAll(".log .hit-numbers"), n => Assert.Equal("hit-damage", n.Children[0].ClassName));
        Assert.All(cut.FindAll(".log .hit-numbers"), n => Assert.StartsWith("hp-bar hit-bar", n.Children[1].ClassName));

        static LogLine Hit(int before, int after, int damage, params LogPart[] extras) => new(
            [
                new LogPart("Bisasam", 0, 0, IsSpecies: true), new LogPart(" (", 0, 0), new LogPart("Ash", 0, 0, IsOwner: true), new LogPart(", HP 90)", 0, 0),
                new LogPart(" hat ", TextWithBar: " trifft "),
                new LogPart("Raupy", 1, 0, IsSpecies: true), new LogPart(" (", 1, 0), new LogPart("Misty", 1, 0, IsOwner: true),
                new LogPart($", HP {before} → {after}", 1, 0, IsTargetHp: true), new LogPart(")", 1, 0),
                new LogPart($" {damage} Schaden zugefügt.", TextWithBar: "."), .. extras,
            ],
            Hit: new LogHit(1, 0, before, after, damage));
    }

    [Fact]
    public void A_hit_with_a_damage_the_page_cannot_show_keeps_the_number_in_the_sentence()
    {
        Put(
            SitePaths.Battle(11),
            BattlePage(11) with
            {
                Sides =
                [
                    new BattleSide("Ash", true, [new BattleFighter(Bisasam, "Ash", "11", 90)]),
                    new BattleSide("Misty", false, [new BattleFighter(Raupy, "Misty", "12", 80)]),
                ],
                Lines = ["Ash hat Raupy (Misty, HP 30 → 10) 20 Schaden zugefügt."],
                Log =
                [
                    new LogLine(
                        [
                            new LogPart("Ash"), new LogPart(" hat ", TextWithBar: " trifft "), new LogPart("Raupy", 1, 0, IsSpecies: true), new LogPart(" (Misty"),
                            new LogPart(", HP 30 → 10", 1, 0, IsTargetHp: true), new LogPart(")"), new LogPart(" 20 Schaden zugefügt.", TextWithBar: "."),
                        ],
                        Hit: new LogHit(1, 0, 30, 10, -20)),
                ],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 11));

        cut.WaitForAssertion(() => Assert.Equal("Ash hat Raupy (Misty) 20 Schaden zugefügt.", cut.Find(".log .hit-text").TextContent));
        Assert.Empty(cut.FindAll(".log .hit-damage"));
        Assert.Single(cut.FindAll(".log .hit-bar"));
    }

    [Theory]
    [InlineData(1, 5, 30, 10)] // a fighter the page does not have
    [InlineData(1, 0, 90, 10)] // more HP before than the target has
    [InlineData(1, 0, 10, 20)] // more HP after than before
    [InlineData(-1, 0, 30, 10)] // negative values from a file not ours must not throw (review 1.8.1, C12)
    [InlineData(1, -1, 30, 10)]
    [InlineData(2, 0, 30, 10)]
    [InlineData(1, 0, 30, -5)]
    public void A_hit_the_page_cannot_draw_keeps_its_full_sentence(int side, int fighter, int before, int after)
    {
        Put(
            SitePaths.Battle(9),
            BattlePage(9) with
            {
                Sides =
                [
                    new BattleSide("Ash", true, [new BattleFighter(Bisasam, "Ash", "11", 90)]),
                    new BattleSide("Misty", false, [new BattleFighter(Raupy, "Misty", "12", 80)]),
                ],
                Lines = ["Raupy (Misty, HP 30 → 10) 20 Schaden."],
                Log =
                [
                    new LogLine(
                        [new LogPart("Raupy", 1, 0, IsSpecies: true), new LogPart(" (Misty"), new LogPart(", HP 30 → 10", 1, 0, IsTargetHp: true), new LogPart(") 20 Schaden.", TextWithBar: ").")],
                        Hit: new LogHit(side, fighter, before, after, 20)),
                ],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 9));

        cut.WaitForAssertion(() => Assert.Equal("Raupy (Misty, HP 30 → 10) 20 Schaden.", cut.Find(".log li").TextContent));
        Assert.Empty(cut.FindAll(".log .hit-bar"));
    }

    [Fact]
    public void Parts_naming_a_position_the_page_lacks_are_plain_text()
    {
        Put(
            SitePaths.Battle(10),
            BattlePage(10) with
            {
                Sides = [new BattleSide("Ash", true, [new BattleFighter(Bisasam, "Ash", "11", 90, Color: "#FFFF00")])],
                Lines = ["Bisasam Raupy Karpador"],
                Log = [new LogLine([new LogPart("Bisasam", -1, 0, IsSpecies: true), new LogPart(" Raupy", 0, -1, IsSpecies: true), new LogPart(" Karpador", 5, 0, IsSpecies: true)])],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 10));

        // A foreign file must not break the page (review 1.8.1, C12).
        cut.WaitForAssertion(() => Assert.Equal("Bisasam Raupy Karpador", cut.Find(".log li").TextContent));
        Assert.Empty(cut.FindAll(".log .tinted"));
    }

    [Fact]
    public void A_quarter_of_the_hp_left_counts_as_low_on_the_card_and_in_the_log()
    {
        Put(
            SitePaths.Battle(11),
            BattlePage(11) with
            {
                Sides =
                [
                    new BattleSide("Ash", true, [new BattleFighter(Bisasam, "Ash", "11", 90, HpLeft: 90, Used: true)]),
                    new BattleSide("Misty", false, [new BattleFighter(Raupy, "Misty", "12", 80, HpLeft: 20, Used: true)]),
                ],
                Lines = ["Raupy (Misty, HP 80 → 20) 60 Schaden."],
                Log =
                [
                    new LogLine(
                        [new LogPart("Raupy", 1, 0, IsSpecies: true), new LogPart(" (Misty"), new LogPart(", HP 80 → 20", 1, 0, IsTargetHp: true), new LogPart(") 60 Schaden.")],
                        Hit: new LogHit(1, 0, 80, 20)),
                ],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 11));

        // Exactly 25 % is low: 20 × 4 ≤ 80 (review 1.8.1, C12).
        cut.WaitForAssertion(() => Assert.Contains("low", cut.Find(".log .hit-bar").ClassName));
        Assert.Equal(["hp-bar", "hp-bar low"], cut.FindAll(".fighters .hp-bar").Select(b => b.ClassName));
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
}
