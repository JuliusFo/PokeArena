using PokeArena.Contracts;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests.Game;

/// <summary>Headline and result of a stored fight, as the website and the desktop app show them.</summary>
public class BattleTextsTests
{
    private const int Arkani = 59;
    private const int Pikachu = 25;
    private const int Karpador = 129;

    private static readonly BattleTexts Texts = new(GameFixture.Species, GameFixture.Npcs);

    [Fact]
    public void A_defeated_boss_lists_the_survivors_as_catchers_including_deleted_trainers()
    {
        var log = new BattleLog(
            [
                new LogFighter(0, Pikachu, 35, "1", "Ash"),
                new LogFighter(0, Pikachu, 35, null, BattleLog.DeletedOwner),
                new LogFighter(0, Pikachu, 35, "3", "Misty"),
                new LogFighter(1, Arkani, 200, null, "Boss"),
            ],
            [new BattleEvent(BattleEventKind.Fainted, 1, Fighter: 2), new BattleEvent(BattleEventKind.Fainted, 2, Fighter: 3)]);

        var texts = Texts.Describe(Battle(BattleKind.Boss, BattleOutcome.BossDefeated, boss: Arkani), log);

        Assert.Equal("Boss-Kampf gegen Arkani (selten)", texts.Headline);
        Assert.Equal($"Arkani wurde besiegt! Gefangen von: Ash, {BattleLog.DeletedOwner}.", texts.Result);
        Assert.True(texts.TrainerSideWon);
    }

    [Fact]
    public void An_escaped_boss_says_so()
    {
        var log = new BattleLog([new LogFighter(0, Pikachu, 35, "1", "Ash"), new LogFighter(1, Arkani, 200, null, "Boss")], []);

        var texts = Texts.Describe(Battle(BattleKind.Boss, BattleOutcome.BossEscaped, boss: Arkani), log);

        Assert.Equal(("Arkani ist entkommen.", false), (texts.Result, texts.TrainerSideWon));
    }

    [Theory]
    [InlineData(BattleOutcome.ChallengerWon, "Ash besiegt Misty und bekommt Karpador von Misty!")]
    [InlineData(BattleOutcome.OpponentWon, "Misty besiegt Ash und bekommt Karpador von Ash!")]
    public void A_pvp_fight_names_challenger_opponent_and_loot(BattleOutcome outcome, string result)
    {
        var log = new BattleLog([new LogFighter(0, Pikachu, 35, "1", "Ash"), new LogFighter(1, Karpador, 30, "2", "Misty")], []);

        var texts = Texts.Describe(Battle(BattleKind.Pvp, outcome, loot: Karpador), log);

        Assert.Equal(("Ash gegen Misty", result), (texts.Headline, texts.Result));
    }

    [Theory]
    [InlineData(BattleOutcome.ChallengerWon, Karpador, 1, "Ash besiegt Eich zum ersten Mal und bekommt den Orden, 1 Token und Karpador!")]
    [InlineData(BattleOutcome.ChallengerWon, null, 3, "Ash besiegt Eich zum ersten Mal und bekommt den Orden, 3 Tokens!")]
    [InlineData(BattleOutcome.ChallengerWon, null, null, "Ash besiegt Eich.")]
    [InlineData(BattleOutcome.OpponentWon, null, null, "Eich besiegt Ash.")]
    public void An_npc_fight_names_the_reward_of_a_first_win_only(BattleOutcome outcome, int? reward, int? tokens, string result)
    {
        var log = new BattleLog([new LogFighter(0, Pikachu, 35, "1", "Ash"), new LogFighter(1, Pikachu, 35, null, "Eich")], []);

        var texts = Texts.Describe(Battle(BattleKind.Npc, outcome, loot: reward, npc: "eich", tokens: tokens), log);

        Assert.Equal(("Ash gegen Eich", result), (texts.Headline, texts.Result));
    }

    private static Battle Battle(BattleKind kind, BattleOutcome outcome, int? boss = null, int? loot = null, string? npc = null, int? tokens = null) => new()
    {
        Kind = kind,
        Outcome = outcome,
        BossSpeciesId = boss,
        LootSpeciesId = loot,
        NpcId = npc,
        Tokens = tokens,
        LogJson = "",
    };
}
