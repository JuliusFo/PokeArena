using PokeArena.Contracts;
using PokeArena.Web.Services;

namespace PokeArena.Web.Tests;

public class StatisticsBarsTests
{
    [Fact]
    public void Trainer_bars_measure_against_the_longest_and_take_the_darkened_chat_colour()
    {
        var bars = StatisticsBars.BossParticipations(
        [
            new TrainerCount(1, new TrainerRef("11", "Anna", "#FFFF00"), 4),
            new TrainerCount(2, new TrainerRef("12", "Ben"), 1),
        ]);

        Assert.Equal(
            [
                new Bar("1.", "Anna", null, "4", "100", Tint.Darkened("#ffff00"), "trainer/11"),
                new Bar("2.", "Ben", null, "1", "25", Bar.Neutral, "trainer/12"),
            ],
            bars);
    }

    [Fact]
    public void A_colour_that_is_no_colour_never_reaches_the_style()
    {
        var bar = StatisticsBars.BadLuck([new BadLuckStreak(1, new TrainerRef("11", "Anna", "red; background: url(x)"), 3, false)]).Single();

        Assert.Equal(Bar.Neutral, bar.Fill);
    }

    [Fact]
    public void Rivals_and_the_community_use_the_green_of_several_colours()
    {
        var rival = StatisticsBars.Rivals(
            [new Rivalry(1, new TrainerRef("12", "Ben", "#1E90FF"), new TrainerRef("11", "Anna", "#FF69B4"), 1, 1, 0)]).Single();
        var community = StatisticsBars.Community(new CommunityTotals(38, 151, 4, 27, 31));

        Assert.Equal(new Bar("1.", "Ben – Anna", null, "1 Kampf · 1 : 0", "100", Bar.Mixed), rival);
        Assert.Equal(new Bar(null, "Arten", null, "38 / 151", "25.2", Bar.Mixed), community);
    }

    [Fact]
    public void Bosses_take_the_colour_of_their_first_type()
    {
        var bar = StatisticsBars.Bosses([new BossTally(1, new SpeciesInfo(59, "Arkani", ["Feuer"]), 3)]).Single();

        Assert.Equal(Tint.Darkened(Tint.TypeColors["type-feuer"]), bar.Fill);
    }

    [Fact]
    public void Trainer_ids_are_escaped_in_links() =>
        Assert.Equal("trainer/a%2Fb", StatisticsBars.TrainerLink(new TrainerRef("a/b", "X")));
}
