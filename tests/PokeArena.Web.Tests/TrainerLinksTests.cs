using PokeArena.Contracts;
using PokeArena.Web.Services;

namespace PokeArena.Web.Tests;

public class TrainerLinksTests
{
    private static readonly SpeciesInfo Bisasam = new(1, "Bisasam", ["Pflanze", "Gift"]);
    private static readonly SpeciesInfo Raupy = new(10, "Raupy", ["Käfer"]);

    private static BattlePage Page(BattleKind kind, params BattleSide[] sides) =>
        new(1, kind, DateTimeOffset.UnixEpoch, 1, "Kampf", "Ergebnis", sides, []);

    [Fact]
    public void Trainer_ids_are_escaped_in_links() =>
        Assert.Equal("trainer/a%2Fb", TrainerLinks.Href("a/b"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Without_an_id_there_is_no_link(string? id) =>
        Assert.Null(TrainerLinks.Href(id));

    [Fact]
    public void A_side_of_one_trainer_is_that_trainer()
    {
        var page = Page(
            BattleKind.Pvp,
            new BattleSide("Anna", true, [new BattleFighter(Bisasam, "Anna", "1", 90, Color: "#FFFF00"), new BattleFighter(Raupy, "Anna", "1", 45, Color: "#FFFF00")]),
            new BattleSide("Ben", false, [new BattleFighter(Raupy, "Ben", "2", 45)]));

        Assert.Equal([new TrainerRef("1", "Anna", "#FFFF00"), new TrainerRef("2", "Ben")], [TrainerLinks.OfSide(page, 0), TrainerLinks.OfSide(page, 1)]);
        Assert.Null(TrainerLinks.OfSide(page, 2));
    }

    [Fact]
    public void A_side_with_two_ids_or_another_owner_is_no_trainer()
    {
        var page = Page(
            BattleKind.Pvp,
            new BattleSide("Anna", true, [new BattleFighter(Bisasam, "Anna", "1", 90), new BattleFighter(Raupy, "Anna", "3", 45)]),
            new BattleSide("Ben", false, [new BattleFighter(Raupy, "Ben", "2", 45), new BattleFighter(Bisasam, "Clara", "2", 90)]));

        Assert.Null(TrainerLinks.OfSide(page, 0));
        Assert.Null(TrainerLinks.OfSide(page, 1));
    }

    [Fact]
    public void The_group_of_a_boss_fight_is_no_trainer_even_for_a_viewer_named_trainer() =>
        Assert.Null(TrainerLinks.OfSide(
            Page(
                BattleKind.Boss,
                new BattleSide("Trainer", true, [new BattleFighter(Bisasam, "Trainer", "1", 90)]),
                new BattleSide("Relaxo", false, [new BattleFighter(Raupy, "Boss", null, 400)])),
            0));

    [Fact]
    public void An_npc_and_a_deleted_trainer_are_no_trainer()
    {
        var page = Page(
            BattleKind.Npc,
            new BattleSide("[gelöscht]", false, [new BattleFighter(Bisasam, "[gelöscht]", null, 90)]),
            new BattleSide("Rocko", true, [new BattleFighter(Raupy, "Rocko", null, 45)]));

        Assert.Null(TrainerLinks.OfSide(page, 0));
        Assert.Null(TrainerLinks.OfSide(page, 1));
    }
}
