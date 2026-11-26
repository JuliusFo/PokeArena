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
        Assert.Equal("Ash – gewonnen", cut.Find(".side h2").TextContent.Trim());
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
        // Each side's total links its trainer like the statistics page, in both clipped copies (claude/kampf-trainer-link-3fc5e0).
        Assert.Equal(
            ["trainer/11", "trainer/11", "trainer/12", "trainer/12"],
            box.QuerySelectorAll("ul a").Select(a => a.GetAttribute("href")));
    }

    [Fact]
    public void A_card_of_one_trainer_links_the_trainer_page_in_its_heading()
    {
        Put(
            SitePaths.Battle(9),
            BattlePage(9) with
            {
                Sides =
                [
                    new BattleSide("Ash", true, [new BattleFighter(Bisasam, "Ash", "11", 40, Color: "#FFFF00")]),
                    new BattleSide("Misty", false, [new BattleFighter(Raupy, "Misty", "12", 30)]),
                ],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 9));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".side h2").Count));
        Assert.Equal(["Ash – gewonnen", "Misty"], cut.FindAll(".side h2").Select(h => h.TextContent.Trim()));
        // In the chat colour like the names on the card; without a colour the link keeps the link colour.
        Assert.Equal(
            [("trainer/11", "Ash", "tinted", Tint.Style("#FFFF00")), ("trainer/12", "Misty", null, null)],
            cut.FindAll(".side h2 a").Select(a => (a.GetAttribute("href"), a.TextContent, a.GetAttribute("class"), a.GetAttribute("style"))));
        // The owner is in the heading: the rows name no owner of their own.
        Assert.Empty(cut.FindAll(".fighters a"));
    }

    [Fact]
    public void Group_boss_npc_and_deleted_trainer_headings_are_no_links()
    {
        var arkani = new SpeciesInfo(59, "Arkani", ["Feuer"]);
        Put(
            SitePaths.Battle(10),
            BattlePage(10) with
            {
                Kind = BattleKind.Boss,
                Sides =
                [
                    new BattleSide("Trainer", true, [new BattleFighter(Bisasam, "Trainer", "a/b", 40), new BattleFighter(Raupy, "Trainer", "a/b", 30)]),
                    new BattleSide("Arkani", false, [new BattleFighter(arkani, "Boss", null, 400)]),
                ],
            },
            SiteJson.Default.BattlePage);
        Put(
            SitePaths.Battle(11),
            BattlePage(11) with
            {
                Kind = BattleKind.Npc,
                Sides =
                [
                    new BattleSide("[gelöscht]", false, [new BattleFighter(Bisasam, "[gelöscht]", null, 40)]),
                    new BattleSide("Rocko", true, [new BattleFighter(arkani, "Rocko", null, 60)]),
                ],
            },
            SiteJson.Default.BattlePage);

        var boss = Render<Battle>(p => p.Add(b => b.Id, 10));
        var npc = Render<Battle>(p => p.Add(b => b.Id, 11));

        boss.WaitForAssertion(() => Assert.Equal(2, boss.FindAll(".side h2").Count));
        npc.WaitForAssertion(() => Assert.Equal(2, npc.FindAll(".side h2").Count));
        // The group of a boss fight is no trainer, even with a single viewer named „Trainer“ in it: the rows keep their
        // own owner links, escaped like everywhere (review claude/kampf-trainer-link-3fc5e0).
        Assert.Empty(boss.FindAll(".side h2 a"));
        Assert.Equal(
            [("trainer/a%2Fb", "Trainer"), ("trainer/a%2Fb", "Trainer")],
            boss.FindAll(".fighters a").Select(a => (a.GetAttribute("href"), a.TextContent)));
        Assert.Empty(npc.FindAll(".side h2 a"));
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
        // The first hit leaves 22 of 80 (half or less: orange), the second is more than the 22 left (the bar loses 22,
        // the text keeps 31), the third took nothing (no minimum-width lost piece then).
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
                ("hp-bar hit-bar mid", "--hp: 27.5%; --before: 100%", "HP 80 → 22 von 80", "HP 80 → 22"),
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
        // The second hit is more than the 22 left: the number is the full hit (−31) beside the bar 22 → 0, so an overkill
        // shows (user decision 2026-11-27, viewer feedback; 1.8.2 to 1.10.0 showed −22).
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
        Assert.Equal([" −58 Schaden, ", " −31 Schaden, "], cut.FindAll(".log .hit-damage").Select(d => d.TextContent));
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

    /// <summary>A fight with two hits in the current format: the attacker's HP and the damage are parts of their own.</summary>
    private void PutTextViewBattle(long id)
    {
        Put(
            SitePaths.Battle(id),
            BattlePage(id) with
            {
                Sides =
                [
                    new BattleSide("Ash", true, [new BattleFighter(Bisasam, "Ash", "11", 90)]),
                    new BattleSide("Misty", false, [new BattleFighter(Raupy, "Misty", "12", 80)]),
                ],
                Lines =
                [
                    "Runde 1",
                    "Bisasam (Ash, HP 40) hat Raupy (Misty, HP 80 → 22) 58 Schaden zugefügt. Volltreffer! Sehr effektiv!",
                    "Bisasam (Ash, HP 90) hat Raupy (Misty, HP 22 → 0) 31 Schaden zugefügt.",
                ],
                Log =
                [
                    new LogLine([new LogPart("Runde 1")], IsRound: true),
                    Hit(40, 80, 22, 58, new LogPart(" Volltreffer!", IsCritical: true), new LogPart(" Sehr effektiv!")),
                    Hit(90, 22, 0, 31),
                ],
            },
            SiteJson.Default.BattlePage);

        static LogLine Hit(int attackerHp, int before, int after, int damage, params LogPart[] extras) => new(
            [
                new LogPart("Bisasam", 0, 0, IsSpecies: true), new LogPart(" (", 0, 0), new LogPart("Ash", 0, 0, IsOwner: true),
                new LogPart(", HP ", 0, 0), new LogPart($"{attackerHp}", 0, 0, IsHp: true), new LogPart(")", 0, 0),
                new LogPart(" hat ", TextWithBar: " trifft "),
                new LogPart("Raupy", 1, 0, IsSpecies: true), new LogPart(" (", 1, 0), new LogPart("Misty", 1, 0, IsOwner: true),
                new LogPart($", HP {before} → {after}", 1, 0, IsTargetHp: true), new LogPart(")", 1, 0),
                new LogPart($" {damage} Schaden zugefügt.", TextWithBar: ".", IsDamage: true), .. extras,
            ],
            Hit: new LogHit(1, 0, before, after, damage));
    }

    [Fact]
    public void The_text_view_shows_each_hit_as_one_sentence_with_coloured_numbers()
    {
        // The viewer picked the text view before (claude/kampflog-design-options-cb7042). The knockout shows the full
        // hit (31 on 22 left), not what arrived (user decision 2026-11-27, viewer feedback).
        JSInterop.Setup<string?>("localStorage.getItem", ViewerSettings.LogViewKey).SetResult("text");
        PutTextViewBattle(20);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 20));

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".log li").Count));
        Assert.Equal(
            [
                "Runde 1",
                "Bisasam (Ash, HP 40) trifft Raupy (Misty, HP 80 → 22) und verursacht 58 Schaden. Volltreffer! Sehr effektiv!",
                "Bisasam (Ash, HP 90) trifft Raupy (Misty, HP 22 → 0) und verursacht 31 Schaden.",
            ],
            cut.FindAll(".log li").Select(li => li.TextContent));
        Assert.Empty(cut.FindAll(".log .hit-bar"));
        Assert.Empty(cut.FindAll(".log .hit-damage"));
        // 40 of 90 is under half, 80 of 80 full, 22 of 80 above a quarter, 0 low; the fighters' own maximum counts.
        Assert.Equal(
            [("40", "log-hp mid"), ("80", "log-hp high"), ("22", "log-hp mid"), ("90", "log-hp high"), ("22", "log-hp mid"), ("0", "log-hp low")],
            cut.FindAll(".log .log-hp").Select(s => (s.TextContent, s.ClassName)));
        Assert.Equal(["58", "31"], cut.FindAll(".log strong.log-damage").Select(s => s.TextContent));
        Assert.Equal("Volltreffer!", cut.Find(".log .critical").TextContent);
        Assert.Equal("true", cut.Find(".view-switch button:last-child").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void The_text_view_shows_what_arrived_when_the_damage_is_foreign()
    {
        // A negative damage is not ours: the text view falls back to what arrived (30 → 10) instead of „−20“.
        JSInterop.Setup<string?>("localStorage.getItem", ViewerSettings.LogViewKey).SetResult("text");
        Put(
            SitePaths.Battle(30),
            BattlePage(30) with
            {
                Sides =
                [
                    new BattleSide("Ash", true, [new BattleFighter(Bisasam, "Ash", "11", 90)]),
                    new BattleSide("Misty", false, [new BattleFighter(Raupy, "Misty", "12", 80)]),
                ],
                Lines = ["Bisasam (Ash, HP 90) hat Raupy (Misty, HP 30 → 10) 20 Schaden zugefügt."],
                Log =
                [
                    new LogLine(
                        [
                            new LogPart("Bisasam", 0, 0, IsSpecies: true), new LogPart(" (Ash, HP 90)", 0, 0), new LogPart(" hat ", TextWithBar: " trifft "),
                            new LogPart("Raupy", 1, 0, IsSpecies: true), new LogPart(" (Misty", 1, 0),
                            new LogPart(", HP 30 → 10", 1, 0, IsTargetHp: true), new LogPart(")", 1, 0),
                            new LogPart(" 20 Schaden zugefügt.", TextWithBar: ".", IsDamage: true),
                        ],
                        Hit: new LogHit(1, 0, 30, 10, -20)),
                ],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 30));

        cut.WaitForAssertion(() => Assert.Equal("20", cut.Find(".log strong.log-damage").TextContent));
    }

    [Fact]
    public void The_bar_view_colours_the_attackers_hp_like_the_text_view()
    {
        // Viewer feedback (claude/hp-coloring-bar-view-4f3c63): the attacker's HP in the sentence get the text view's
        // colours, and the bars its orange step.
        PutTextViewBattle(27);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 27));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".log .hit-bar").Count));
        Assert.Equal(
            ["Bisasam (Ash, HP 40) trifft Raupy (Misty). Volltreffer! Sehr effektiv!", "Bisasam (Ash, HP 90) trifft Raupy (Misty)."],
            cut.FindAll(".log .hit-text").Select(t => t.TextContent));
        // 40 of 90 is under half, 90 of 90 full; the target's HP stay in the bar, not as coloured text.
        Assert.Equal(
            [("40", "log-hp mid"), ("90", "log-hp high")],
            cut.FindAll(".log .log-hp").Select(s => (s.TextContent, s.ClassName)));
        Assert.All(cut.FindAll(".log .log-hp"), s => Assert.Equal("hit-text", s.ParentElement!.ClassName));
        // 22 of 80 left is orange, 0 red.
        Assert.Equal(["hp-bar hit-bar mid", "hp-bar hit-bar low"], cut.FindAll(".log .hit-bar").Select(b => b.ClassName));
    }

    [Fact]
    public void Half_the_hp_left_counts_as_mid_on_the_card_and_in_the_log()
    {
        // The text view's orange step, now on the bars too: exactly half is orange, one more is green.
        Put(
            SitePaths.Battle(28),
            BattlePage(28) with
            {
                Sides =
                [
                    new BattleSide("Ash", true, [new BattleFighter(Bisasam, "Ash", "11", 90, HpLeft: 45, Used: true)]),
                    new BattleSide("Misty", false, [new BattleFighter(Raupy, "Misty", "12", 80, HpLeft: 41, Used: true)]),
                ],
                Lines = ["Raupy (Misty, HP 80 → 40) 40 Schaden.", "Raupy (Misty, HP 80 → 41) 39 Schaden."],
                Log = [Hit(40), Hit(41)],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 28));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".log .hit-bar").Count));
        Assert.Equal(["hp-bar hit-bar mid", "hp-bar hit-bar"], cut.FindAll(".log .hit-bar").Select(b => b.ClassName));
        Assert.Equal(["hp-bar mid", "hp-bar"], cut.FindAll(".fighters .hp-bar").Select(b => b.ClassName));

        static LogLine Hit(int after) => new(
            [new LogPart("Raupy", 1, 0, IsSpecies: true), new LogPart(" (Misty"), new LogPart($", HP 80 → {after}", 1, 0, IsTargetHp: true), new LogPart($") {80 - after} Schaden.")],
            Hit: new LogHit(1, 0, 80, after));
    }

    [Fact]
    public void Huge_hp_from_a_file_not_ours_keep_their_colour()
    {
        // 1.5 billion of int.MaxValue is above half: green. Times 4 in int would wrap and turn it red (review
        // claude/hp-coloring-bar-view-4f3c63).
        Put(
            SitePaths.Battle(29),
            BattlePage(29) with
            {
                Sides =
                [
                    new BattleSide("Ash", true, [new BattleFighter(Bisasam, "Ash", "11", int.MaxValue, HpLeft: 1_500_000_000, Used: true)]),
                    new BattleSide("Misty", false, [new BattleFighter(Raupy, "Misty", "12", 30, HpLeft: 0, Used: true)]),
                ],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 29));

        cut.WaitForAssertion(() => Assert.Equal("hp-bar", cut.Find(".fighters .hp-bar").ClassName));
    }

    [Fact]
    public void The_switch_changes_the_view_and_the_browser_remembers_it()
    {
        PutTextViewBattle(21);
        var cut = Render<Battle>(p => p.Add(b => b.Id, 21));

        // Without a pick: the bars.
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".log .hit-bar").Count));
        var buttons = cut.FindAll(".view-switch button");
        Assert.Equal([("Balken", "true"), ("Text", "false")], buttons.Select(b => (b.TextContent, b.GetAttribute("aria-pressed"))));

        cut.Find(".view-switch button:last-child").Click();

        Assert.Empty(cut.FindAll(".log .hit-bar"));
        Assert.Equal(2, cut.FindAll(".log strong.log-damage").Count);
        Assert.Equal([ViewerSettings.LogViewKey, "text"], JSInterop.VerifyInvoke("localStorage.setItem").Arguments);

        cut.Find(".view-switch button:first-child").Click();

        Assert.Equal(2, cut.FindAll(".log .hit-bar").Count);
        Assert.Equal([ViewerSettings.LogViewKey, "balken"], JSInterop.VerifyInvoke("localStorage.setItem", calledTimes: 2)[1].Arguments);
    }

    [Fact]
    public void Blocked_storage_starts_with_the_bars_and_the_switch_still_works()
    {
        JSInterop.Setup<string?>("localStorage.getItem", ViewerSettings.LogViewKey).SetException(new Microsoft.JSInterop.JSException("SecurityError"));
        JSInterop.SetupVoid("localStorage.setItem", _ => true).SetException(new Microsoft.JSInterop.JSException("SecurityError"));
        PutTextViewBattle(22);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 22));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".log .hit-bar").Count));
        cut.Find(".view-switch button:last-child").Click();
        Assert.Empty(cut.FindAll(".log .hit-bar"));
    }

    [Fact]
    public void The_text_view_keeps_the_wording_of_an_older_file()
    {
        // 1.8.2 and older: no damage part marked, the attacker's HP inside the label's last part. The sentence stays
        // as written; only the target's HP get their colour.
        JSInterop.Setup<string?>("localStorage.getItem", ViewerSettings.LogViewKey).SetResult("text");
        Put(
            SitePaths.Battle(23),
            BattlePage(23) with
            {
                Sides =
                [
                    new BattleSide("Ash", true, [new BattleFighter(Bisasam, "Ash", "11", 90)]),
                    new BattleSide("Misty", false, [new BattleFighter(Raupy, "Misty", "12", 80)]),
                ],
                Lines = ["Bisasam (Ash, HP 90) hat Raupy (Misty, HP 80 → 22) 58 Schaden zugefügt."],
                Log =
                [
                    new LogLine(
                        [
                            new LogPart("Bisasam", 0, 0, IsSpecies: true), new LogPart(" (", 0, 0), new LogPart("Ash", 0, 0, IsOwner: true), new LogPart(", HP 90)", 0, 0),
                            new LogPart(" hat ", TextWithBar: " trifft "),
                            new LogPart("Raupy", 1, 0, IsSpecies: true), new LogPart(" (", 1, 0), new LogPart("Misty", 1, 0, IsOwner: true),
                            new LogPart(", HP 80 → 22", 1, 0, IsTargetHp: true), new LogPart(")", 1, 0),
                            new LogPart(" 58 Schaden zugefügt.", TextWithBar: "."),
                        ],
                        Hit: new LogHit(1, 0, 80, 22, 58)),
                ],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 23));

        cut.WaitForAssertion(() => Assert.Equal("Bisasam (Ash, HP 90) hat Raupy (Misty, HP 80 → 22) 58 Schaden zugefügt.", cut.Find(".log li").TextContent));
        Assert.Equal(["80", "22"], cut.FindAll(".log .log-hp").Select(s => s.TextContent));
        Assert.Empty(cut.FindAll(".log .log-damage"));
    }

    [Theory]
    [InlineData("abc", 0, true)] // not a number
    [InlineData("-5", 0, true)] // negative
    [InlineData("95", 0, true)] // more than the 90 the fighter has
    [InlineData("40", 5, true)] // a fighter the page does not have
    [InlineData("abc", 0, false)] // the same in the bar view (claude/hp-coloring-bar-view-4f3c63)
    [InlineData("-5", 0, false)]
    [InlineData("95", 0, false)]
    [InlineData("40", 5, false)]
    public void An_attacker_hp_the_page_cannot_read_stays_plain_text(string hp, int fighter, bool textView)
    {
        // A file not ours (review claude/kampflog-design-options-cb7042): the part stays as written, uncoloured; the
        // target's HP and the damage still come from the hit the page accepted.
        if (textView)
        {
            JSInterop.Setup<string?>("localStorage.getItem", ViewerSettings.LogViewKey).SetResult("text");
        }

        Put(
            SitePaths.Battle(25),
            BattlePage(25) with
            {
                Sides =
                [
                    new BattleSide("Ash", true, [new BattleFighter(Bisasam, "Ash", "11", 90)]),
                    new BattleSide("Misty", false, [new BattleFighter(Raupy, "Misty", "12", 80)]),
                ],
                Lines = [$"Bisasam (Ash, HP {hp}) hat Raupy (Misty, HP 80 → 22) 58 Schaden zugefügt."],
                Log =
                [
                    new LogLine(
                        [
                            new LogPart("Bisasam", 0, 0, IsSpecies: true), new LogPart(" (Ash, HP ", 0, 0), new LogPart(hp, 0, fighter, IsHp: true),
                            new LogPart(")", 0, 0), new LogPart(" hat ", TextWithBar: " trifft "),
                            new LogPart("Raupy", 1, 0, IsSpecies: true), new LogPart(" (Misty", 1, 0),
                            new LogPart(", HP 80 → 22", 1, 0, IsTargetHp: true), new LogPart(")", 1, 0),
                            new LogPart(" 58 Schaden zugefügt.", TextWithBar: ".", IsDamage: true),
                        ],
                        Hit: new LogHit(1, 0, 80, 22, 58)),
                ],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 25));

        if (textView)
        {
            cut.WaitForAssertion(() => Assert.Equal(
                $"Bisasam (Ash, HP {hp}) trifft Raupy (Misty, HP 80 → 22) und verursacht 58 Schaden.", cut.Find(".log li").TextContent));
            Assert.Equal(["80", "22"], cut.FindAll(".log .log-hp").Select(s => s.TextContent));
        }
        else
        {
            cut.WaitForAssertion(() => Assert.Equal($"Bisasam (Ash, HP {hp}) trifft Raupy (Misty).", cut.Find(".log .hit-text").TextContent));
            Assert.Empty(cut.FindAll(".log .log-hp"));
        }
    }

    [Fact]
    public void The_text_view_keeps_the_full_sentence_of_a_hit_the_page_cannot_draw()
    {
        // The hit names a fighter the page does not have: no colours, no new wording — the sentence as written, like
        // the bar view's fallback (review claude/kampflog-design-options-cb7042).
        JSInterop.Setup<string?>("localStorage.getItem", ViewerSettings.LogViewKey).SetResult("text");
        Put(
            SitePaths.Battle(26),
            BattlePage(26) with
            {
                Lines = ["Bisasam (Ash, HP 40) hat Raupy (Misty, HP 30 → 10) 20 Schaden zugefügt."],
                Log =
                [
                    new LogLine(
                        [
                            new LogPart("Bisasam", 0, 0, IsSpecies: true), new LogPart(" (Ash, HP ", 0, 0), new LogPart("40", 0, 0, IsHp: true),
                            new LogPart(")", 0, 0), new LogPart(" hat ", TextWithBar: " trifft "),
                            new LogPart("Raupy (Misty"), new LogPart(", HP 30 → 10", 1, 5, IsTargetHp: true), new LogPart(")"),
                            new LogPart(" 20 Schaden zugefügt.", TextWithBar: ".", IsDamage: true),
                        ],
                        Hit: new LogHit(1, 5, 30, 10, 20)),
                ],
            },
            SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 26));

        cut.WaitForAssertion(() => Assert.Equal(
            "Bisasam (Ash, HP 40) hat Raupy (Misty, HP 30 → 10) 20 Schaden zugefügt.", cut.Find(".log li").TextContent));
        Assert.Empty(cut.FindAll(".log .log-hp"));
        Assert.Empty(cut.FindAll(".log .log-damage"));
        Assert.Empty(cut.FindAll(".log .hit-bar"));
    }

    [Fact]
    public void A_log_without_hits_has_no_view_switch()
    {
        Put(SitePaths.Battle(24), BattlePage(24), SiteJson.Default.BattlePage);

        var cut = Render<Battle>(p => p.Add(b => b.Id, 24));

        cut.WaitForAssertion(() => Assert.Contains("Kampfverlauf", cut.Markup));
        Assert.Empty(cut.FindAll(".view-switch"));
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
