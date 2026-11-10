using PokeArena.Contracts;
using PokeArena.Web.Services;

namespace PokeArena.Web.Tests;

public class DamageMeterTests
{
    private static readonly SpeciesInfo Bisasam = new(1, "Bisasam", ["Pflanze", "Gift"]);
    private static readonly SpeciesInfo Glumanda = new(4, "Glumanda", ["Feuer"]);
    private static readonly SpeciesInfo Raupy = new(10, "Raupy", ["Käfer"]);
    private static readonly SpeciesInfo Relaxo = new(143, "Relaxo", ["Normal"]);

    private static BattlePage Page(BattleKind kind, params BattleSide[] sides) =>
        new(1, kind, DateTimeOffset.UnixEpoch, 1, "Kampf", "Ergebnis", sides, []);

    [Fact]
    public void A_boss_fight_compares_the_sides_and_ranks_the_trainers_with_their_share_of_the_boss()
    {
        var board = DamageMeter.For(Page(
            BattleKind.Boss,
            new BattleSide("Trainer", true,
            [
                new BattleFighter(Bisasam, "Anna", "1", 90, 90, true, "#FFFF00", Damage: 100),
                new BattleFighter(Glumanda, "Clara", "3", 78, 18, true, "#0000FF", Damage: 300),
                new BattleFighter(Raupy, "Ben", "2", 45, 0, true, Damage: 0),
            ]),
            new BattleSide("Relaxo", false, [new BattleFighter(Relaxo, "Boss", null, 400, 0, true, Damage: 200)])))!;

        Assert.Equal(
            [
                new DamageMeter.Bar(null, "Trainer", "(3)", "400", "100", DamageMeter.Mixed, "#ffffff"),
                new DamageMeter.Bar(null, "Relaxo", null, "200", "50", Tint.TypeColors["type-normal"], "#000000"),
            ],
            board.Totals);
        Assert.Equal("Trainer", board.RankingTitle);
        Assert.Equal(
            [
                new DamageMeter.Bar("1.", "Clara", "Glumanda", "300 · 75 %", "100", "#0000ff", "#ffffff"),
                new DamageMeter.Bar("2.", "Anna", "Bisasam", "100 · 25 %", "33.3", "#ffff00", "#000000"),
                new DamageMeter.Bar("3.", "Ben", "Raupy", "0 · 0 %", "0", DamageMeter.Neutral, "#ffffff"),
            ],
            board.Ranking);
    }

    [Fact]
    public void A_team_fight_ranks_every_pokemon_sent_in_and_equal_damage_shares_a_rank()
    {
        var board = DamageMeter.For(Page(
            BattleKind.Pvp,
            new BattleSide("Anna", true,
            [
                new BattleFighter(Bisasam, "Anna", "1", 90, 30, true, "#FFFF00", Damage: 60),
                new BattleFighter(Raupy, "Anna", "1", 45, 45, false, "#FFFF00", Damage: 0),
            ]),
            new BattleSide("Ben", false,
            [
                new BattleFighter(Glumanda, "Ben", "2", 30, 0, true, "#FF4500", Damage: 60),
                new BattleFighter(Raupy, "Ben", "2", 30, 0, true, "#FF4500", Damage: 0),
            ])))!;

        // One trainer per side: the side takes the trainer's colour.
        Assert.Equal([("Anna", "60", "#ffff00"), ("Ben", "60", "#ff4500")], board.Totals.Select(b => (b.Name, b.Value, b.Fill)));
        Assert.Equal("Pokémon", board.RankingTitle);
        Assert.Equal(
            [("1.", "Bisasam", "Anna", "60"), ("1.", "Glumanda", "Ben", "60"), ("3.", "Raupy", "Ben", "0")],
            board.Ranking.Select(b => (b.Rank, b.Name, b.Detail, b.Value)));
    }

    [Fact]
    public void An_npc_fights_in_the_colours_of_its_types()
    {
        var board = DamageMeter.For(Page(
            BattleKind.Npc,
            new BattleSide("Anna", false, [new BattleFighter(Bisasam, "Anna", "1", 90, 0, true, "#zzzzzz", Damage: 10)]),
            new BattleSide("Rocko", true,
            [
                new BattleFighter(Glumanda, "Rocko", null, 30, 30, true, Damage: 50),
                new BattleFighter(Relaxo, "Rocko", null, 30, 30, true, Damage: 40),
            ])))!;

        // An invalid chat colour falls back to grey; two type colours on one side make it the mixed green.
        Assert.Equal([DamageMeter.Neutral, DamageMeter.Mixed], board.Totals.Select(b => b.Fill));
        Assert.Equal(
            [Tint.TypeColors["type-feuer"], Tint.TypeColors["type-normal"], DamageMeter.Neutral],
            board.Ranking.Select(b => b.Fill));
    }

    [Fact]
    public void Absurd_values_neither_overflow_nor_take_the_page_down()
    {
        var board = DamageMeter.For(Page(
            BattleKind.Boss,
            new BattleSide("Trainer", true,
            [
                new BattleFighter(Bisasam, "Anna", "1", 90, 90, true, Damage: int.MaxValue),
                new BattleFighter(Raupy, "Ben", "2", 45, 0, true, Damage: int.MaxValue),
            ]),
            new BattleSide("Relaxo", false,
            [
                new BattleFighter(Relaxo, "Boss", null, int.MaxValue, 0, true, Damage: 1),
                new BattleFighter(Relaxo, "Boss", null, int.MaxValue, 0, true, Damage: 1),
            ])))!;

        Assert.Equal("4294967294", board.Totals[0].Value);
        Assert.Equal("2147483647 · 50 %", board.Ranking[0].Value);
    }

    [Theory]
    [InlineData(-5, 30)]
    [InlineData(5, -30)]
    public void Negative_values_get_no_box(int damage, int maxHp) =>
        Assert.Null(DamageMeter.For(Page(
            BattleKind.Pvp,
            new BattleSide("Anna", true, [new BattleFighter(Bisasam, "Anna", "1", 90, 30, true, Damage: 10)]),
            new BattleSide("Ben", false, [new BattleFighter(Raupy, "Ben", "2", maxHp, 0, true, Damage: damage)]))));

    [Fact]
    public void Files_without_the_damage_get_no_box() =>
        Assert.Null(DamageMeter.For(Page(
            BattleKind.Pvp,
            new BattleSide("Anna", true, [new BattleFighter(Bisasam, "Anna", "1", 90, 30, true)]),
            new BattleSide("Ben", false, [new BattleFighter(Raupy, "Ben", "2", 30, 0, true)]))));
}
