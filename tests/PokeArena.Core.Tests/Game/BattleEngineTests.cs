using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests.Game;

public class BattleEngineTests
{
    /// <summary>No spread, no crits: every hit is exactly Skalierung × Quote × Typfaktor.</summary>
    private static readonly BattleSettings Exact = new() { SpreadMin = 1.0, SpreadMax = 1.0, CritChance = 0 };

    private static SpeciesCatalog Species => GameFixture.Species;

    private static Fighter F(string name, BattleSettings? settings = null, string owner = "Ash", double hpMultiplier = 1.0) =>
        Fighter.Create(Species.Find(name)!, settings ?? Exact, "id-" + owner, owner, hpMultiplier);

    private static DamageCalculator Damage(BattleSettings? settings = null) => new(settings ?? Exact, GameFixture.Types);

    [Fact]
    public void Damage_uses_the_better_of_physical_and_special_and_the_type_factor()
    {
        // Pikachu → Schiggy: physical (55+50)/(65+50) < special (50+50)/(50+50) = 1; Elektro → Wasser 2.
        var hit = Damage().Hit(F("Pikachu"), F("Schiggy"), new BattleRandom(1));

        Assert.Equal(new Hit(40, false, 2.0), hit);
    }

    [Fact]
    public void Damage_is_at_least_one_and_crits_double_it()
    {
        Assert.Equal(1, Damage(Exact with { Scaling = 0.01 }).Hit(F("Karpador"), F("Onix"), new BattleRandom(1)).Damage);

        var crit = Damage(Exact with { CritChance = 1 }).Hit(F("Pikachu"), F("Schiggy"), new BattleRandom(1));
        Assert.Equal(new Hit(80, true, 2.0), crit);
    }

    [Fact]
    public void Spread_stays_within_its_range()
    {
        var damage = Damage(BattleSettings.Default with { CritChance = 0 });
        var random = new BattleRandom(7);

        var hits = Enumerable.Range(0, 500).Select(_ => damage.Hit(F("Pikachu"), F("Schiggy"), random).Damage).ToList();

        Assert.InRange(hits.Min(), 34, 35);
        Assert.Equal(40, hits.Max());
    }

    [Fact]
    public void Boss_hp_scales_with_the_participants()
    {
        // Arkani base HP 90 × HPFaktor 1.0 × BossHPProSpieler 1.5 (1.0.1) × 4 participants = 540.
        var arkani = GameFixture.Species.Find("Arkani")!;

        Assert.Equal(540, BossDraw.Fighter(arkani, BattleSettings.Default, BossSettings.Default, 4).MaxHp);
        Assert.Equal(135, BossDraw.Fighter(arkani, BattleSettings.Default, BossSettings.Default, 1).MaxHp);
    }

    [Theory]
    [InlineData(5, 0.06, 0.20)]
    [InlineData(30, 0.20, 0.40)]
    public void Tier_odds_grow_with_the_participants_up_to_their_caps(int participants, double legendary, double rare)
    {
        Assert.Equal(legendary, BossSettings.Default.Legendary.For(participants), 6);
        Assert.Equal(rare, BossSettings.Default.Rare.For(participants), 6);
    }

    [Fact]
    public void Boss_tiers_follow_the_odds()
    {
        // 30 participants: legendary 20 %, rare 40 %, common 40 % (spec §4). Fixed seeds, so the counts never change.
        var tiers = Enumerable.Range(0, 2000)
            .Select(seed => BossDraw.Species(GameFixture.Species, BossSettings.Default, 30, new BattleRandom(seed)).Rarity)
            .GroupBy(r => r)
            .ToDictionary(g => g.Key, g => g.Count() / 2000.0);

        Assert.InRange(tiers[3], 0.17, 0.23);
        Assert.InRange(tiers[2], 0.37, 0.43);
        Assert.InRange(tiers[1], 0.37, 0.43);
    }

    [Fact]
    public void Same_seed_gives_the_same_fight_and_the_log_survives_json()
    {
        var battle = new BossBattle(Damage(BattleSettings.Default));
        var party = new[] { F("Pikachu", BattleSettings.Default, "Ash"), F("Evoli", BattleSettings.Default, "Misty"), F("Onix", BattleSettings.Default, "Rocko") };
        var boss = F("Arkani", BattleSettings.Default, "Boss", 1.8 * 3);

        var first = battle.Fight(boss, party, 3, new BattleRandom(42));
        var second = battle.Fight(boss, party, 3, new BattleRandom(42));

        Assert.Equal(first.Log.ToJson(), second.Log.ToJson());
        Assert.Equal(first.Log.ToJson(), BattleLog.FromJson(first.Log.ToJson()).ToJson());
        Assert.Equal(first.Survived, second.Survived);
    }

    [Fact]
    public void Boss_attacks_a_third_of_the_participants_rounded_up_each_a_different_one()
    {
        var party = Enumerable.Range(1, 4).Select(i => F("Relaxo", owner: $"T{i}")).ToList();
        var boss = F("Karpador", owner: "Boss", hpMultiplier: 1000);

        var result = new BossBattle(Damage()).Fight(boss, party, 3, new BattleRandom(3));

        var bossHitsRound1 = result.Log.Events
            .Where(e => e is { Kind: BattleEventKind.Attack, Round: 1, Fighter: 4 })
            .Select(e => e.Target)
            .ToList();
        Assert.Equal(2, bossHitsRound1.Count);
        Assert.Equal(2, bossHitsRound1.Distinct().Count());
        Assert.Equal(4, result.Log.Events.Count(e => e is { Kind: BattleEventKind.Attack, Round: 1 } && e.Fighter < 4));
    }

    [Fact]
    public void Survivors_win_when_the_boss_falls_and_nobody_when_all_participants_faint()
    {
        var weakBoss = new BossBattle(Damage()).Fight(F("Karpador", owner: "Boss"), [F("Mewtu", owner: "A"), F("Pikachu", owner: "B")], 3, new BattleRandom(1));
        Assert.True(weakBoss.BossDefeated);
        Assert.Equal([true, true], weakBoss.Survived);
        Assert.Equal(1, weakBoss.Rounds);
        Assert.Equal((0, F("Karpador", owner: "Boss").MaxHp), (weakBoss.BossHp, weakBoss.BossMaxHp));

        var mewtu = F("Mewtu", owner: "Boss", hpMultiplier: 50);
        var strongBoss = new BossBattle(Damage()).Fight(mewtu, [F("Karpador", owner: "A"), F("Raupy", owner: "B")], 3, new BattleRandom(1));
        Assert.False(strongBoss.BossDefeated);
        Assert.Equal([false, false], strongBoss.Survived);

        // The HP left are the boss's HP after the last hit on it.
        var damageToBoss = strongBoss.Log.Events.Where(e => e is { Kind: BattleEventKind.Attack, Target: 2 }).Sum(e => e.Damage);
        Assert.Equal(mewtu.MaxHp, strongBoss.BossMaxHp);
        Assert.Equal(mewtu.MaxHp - damageToBoss, strongBoss.BossHp);
        Assert.InRange(strongBoss.BossHp, 1, mewtu.MaxHp - 1);

        // The website reads the same value back from the stored log.
        Assert.Equal(strongBoss.BossHp, strongBoss.Log.HpLeft(2));
    }

    [Fact]
    public void Round_limit_ends_the_fight_by_remaining_hp()
    {
        var settings = Exact with { MaxRounds = 1 };
        var result = new BossBattle(Damage(settings)).Fight(
            F("Karpador", settings, "Boss", 100), [F("Chaneira", settings, "A")], 3, new BattleRandom(1));

        Assert.True(result.HitRoundLimit);
        Assert.False(result.BossDefeated);
        Assert.Equal(BattleEventKind.RoundLimit, result.Log.Events[^1].Kind);
    }

    [Fact]
    public void Team_fight_challenger_starts_and_the_side_that_lost_a_pokemon_strikes_next()
    {
        var result = new TeamBattle(Damage()).Fight(
            [F("Mewtu", owner: "Ash")],
            [F("Karpador", owner: "Gary"), F("Karpador", owner: "Gary")],
            new BattleRandom(1));

        Assert.True(result.ChallengerWon);
        var attackers = result.Log.Events.Where(e => e.Kind == BattleEventKind.Attack).Select(e => e.Fighter).ToList();
        Assert.Equal([0, 2, 0], attackers);
        Assert.Equal(
            [BattleEventKind.SentOut, BattleEventKind.SentOut, BattleEventKind.Attack, BattleEventKind.Fainted, BattleEventKind.SentOut,
                BattleEventKind.Attack, BattleEventKind.Attack, BattleEventKind.Fainted],
            result.Log.Events.Select(e => e.Kind));
    }

    [Fact]
    public void Log_lines_name_species_owner_and_hp()
    {
        // Skalierung pinned so that Pikachu knocks Schiggy out with its first hit.
        var result = new TeamBattle(Damage(Exact with { Scaling = 25 })).Fight(
            [F("Pikachu", owner: "Ash")], [F("Schiggy", owner: "Misty")], new BattleRandom(1));

        var lines = result.Log.ToLines(Species);

        Assert.Equal("Ash schickt Pikachu in den Kampf.", lines[1]);
        Assert.Equal("Pikachu (Ash, HP 35) hat Schiggy (Misty, HP 0) 50 Schaden zugefügt. Sehr effektiv!", lines[3]);
        Assert.Equal("Schiggy (Misty) wurde besiegt.", lines[4]);
    }
}
