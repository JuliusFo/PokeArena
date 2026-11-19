using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests.Game;

public class BattleEngineTests
{
    /// <summary>No spread, no crits: every hit is exactly Scaling × Ratio × TypeFactor.</summary>
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
        // Arkani base HP 90 × HpFactor 1.0 × BossHpPerParticipant 1.3 (WP-09, 2026-11-19) × 4 participants = 468.
        var arkani = GameFixture.Species.Find("Arkani")!;

        Assert.Equal(468, BossDraw.Fighter(arkani, BattleSettings.Default, BossSettings.Default, 4).MaxHp);
        Assert.Equal(117, BossDraw.Fighter(arkani, BattleSettings.Default, BossSettings.Default, 1).MaxHp);
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
    public void A_participant_fields_each_owned_species_equally_often_however_many_duplicates()
    {
        // 99 × Digda (50) and one Arkani (59): by species, not by specimen. Fixed seeds, so the count never changes.
        int[] owned = [.. Enumerable.Repeat(50, 99), 59];

        var arkani = Enumerable.Range(0, 2000).Count(seed => BossDraw.FieldedSpecies(owned, new BattleRandom(seed)) == 59);

        Assert.InRange(arkani / 2000.0, 0.45, 0.55);
        Assert.Equal(50, BossDraw.FieldedSpecies([50, 50], new BattleRandom(1)));
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
    public void Boss_attacks_a_third_of_the_participants_on_average_each_a_different_one()
    {
        var party = Enumerable.Range(1, 4).Select(i => F("Relaxo", owner: $"T{i}")).ToList();
        var boss = F("Karpador", owner: "Boss", hpMultiplier: 1000);

        var result = new BossBattle(Damage()).Fight(boss, party, 3, new BattleRandom(3));

        List<int?> BossHits(int round) => [.. result.Log.Events
            .Where(e => e.Kind == BattleEventKind.Attack && e.Round == round && e.Fighter == 4)
            .Select(e => e.Target)];
        Assert.Equal([2, 1, 1, 2], Enumerable.Range(1, 4).Select(round => BossHits(round).Count));
        Assert.Equal(2, BossHits(1).Distinct().Count());
        Assert.Equal(4, result.Log.Events.Count(e => e is { Kind: BattleEventKind.Attack, Round: 1 } && e.Fighter < 4));
    }

    [Theory]
    [InlineData(1, new[] { 1, 0, 0, 1, 0, 0 })]
    [InlineData(2, new[] { 1, 1, 0, 1, 1, 0 })]
    [InlineData(3, new[] { 1, 1, 1, 1, 1, 1 })]
    [InlineData(4, new[] { 2, 1, 1, 2, 1, 1 })]
    [InlineData(5, new[] { 2, 2, 1, 2, 2, 1 })]
    [InlineData(6, new[] { 2, 2, 2, 2, 2, 2 })]
    [InlineData(7, new[] { 3, 2, 2, 3, 2, 2 })]
    public void Boss_targets_carry_the_remainder_over_rounding_up_first(int participants, int[] targets)
    {
        // N / 3 per round on average instead of ⌈N / 3⌉ every round (ADR-0006 addendum 2026-11-19).
        Assert.Equal(targets, Enumerable.Range(1, 6).Select(round => BossBattle.TargetsInRound(participants, 3, round)));
    }

    [Theory]
    [InlineData(4, 3, 0)]
    [InlineData(4, 3, -1)]
    [InlineData(0, 3, 1)]
    [InlineData(4, 0, 1)]
    public void Boss_targets_refuse_counts_below_one(int participants, int divisor, int round)
    {
        // TargetsInRound(4, 3, 0) would give 0 instead of the ceiling difference 1: the division truncates to zero.
        Assert.Throws<ArgumentOutOfRangeException>(() => BossBattle.TargetsInRound(participants, divisor, round));
    }

    [Fact]
    public void A_round_without_a_boss_attack_goes_on_and_keeps_its_heading()
    {
        // Two participants: the boss attacks 1, 1, 0, 1 … — round 3 has only the participants' hits.
        var party = new[] { F("Relaxo", owner: "A"), F("Relaxo", owner: "B") };
        var result = new BossBattle(Damage()).Fight(F("Karpador", owner: "Boss", hpMultiplier: 1000), party, 3, new BattleRandom(5));

        int BossHits(int round) => result.Log.Events.Count(e => e.Kind == BattleEventKind.Attack && e.Round == round && e.Fighter == 2);
        Assert.True(result.Rounds >= 4);
        Assert.Equal([1, 1, 0, 1], Enumerable.Range(1, 4).Select(BossHits));
        Assert.Equal(2, result.Log.Events.Count(e => e is { Kind: BattleEventKind.Attack, Round: 3 } && e.Fighter < 2));
        Assert.Contains("Runde 3", result.Log.ToLines(GameFixture.Species));
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
    public void Damage_dealt_counts_only_what_arrived_so_each_side_adds_up_to_the_hp_the_other_lost()
    {
        // Mewtu overkills Karpador with its one hit; that hit counts as Karpador's HP, not as the full damage.
        var karpador = F("Karpador", owner: "Boss");
        var weakBoss = new BossBattle(Damage()).Fight(karpador, [F("Mewtu", owner: "A"), F("Pikachu", owner: "B")], 3, new BattleRandom(1));
        var dealt = weakBoss.Log.DamageDealt();
        Assert.Equal(karpador.MaxHp, dealt[0] + dealt[1]);
        Assert.True(weakBoss.Log.Events.Where(e => e is { Kind: BattleEventKind.Attack, Target: 2 }).Sum(e => e.Damage) > karpador.MaxHp);

        var mewtu = F("Mewtu", owner: "Boss", hpMultiplier: 50);
        var trainers = new[] { F("Karpador", owner: "A"), F("Raupy", owner: "B") };
        var strongBoss = new BossBattle(Damage()).Fight(mewtu, trainers, 3, new BattleRandom(1));
        dealt = strongBoss.Log.DamageDealt();
        Assert.Equal(mewtu.MaxHp - strongBoss.BossHp, dealt[0] + dealt[1]);
        Assert.Equal(trainers.Sum(t => t.MaxHp), dealt[2]);
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
    public void In_a_team_fight_each_side_deals_the_hp_the_other_lost_and_the_unused_deal_nothing()
    {
        // Real spread and crits; Mewtu clears Gary's team before Ash's last two are needed (review feat/damage-meter).
        var settings = BattleSettings.Default;
        var result = new TeamBattle(Damage(settings)).Fight(
            [F("Pikachu", settings, "Ash"), F("Mewtu", settings, "Ash"), F("Raupy", settings, "Ash"), F("Karpador", settings, "Ash")],
            [F("Schiggy", settings, "Gary"), F("Onix", settings, "Gary"), F("Taubsi", settings, "Gary")],
            new BattleRandom(3));
        var log = result.Log;
        var dealt = log.DamageDealt();

        int Lost(int side) => Enumerable.Range(0, log.Fighters.Count).Where(i => log.Fighters[i].Side == side).Sum(i => log.Fighters[i].MaxHp - log.HpLeft(i));
        int Dealt(int side) => Enumerable.Range(0, log.Fighters.Count).Where(i => log.Fighters[i].Side == side).Sum(i => dealt[i]);
        Assert.Equal((Lost(1), Lost(0)), (Dealt(0), Dealt(1)));
        var unused = Enumerable.Range(0, log.Fighters.Count).Where(i => !log.WasUsed(i)).ToList();
        Assert.NotEmpty(unused);
        Assert.All(unused, i => Assert.Equal(0, dealt[i]));
        Assert.True(Enumerable.Range(0, log.Fighters.Count).Count(i => dealt[i] > 0) >= 3);
    }

    [Fact]
    public void Log_lines_name_species_owner_and_hp()
    {
        // Scaling pinned so that Pikachu knocks Schiggy out with its first hit.
        var result = new TeamBattle(Damage(Exact with { Scaling = 25 })).Fight(
            [F("Pikachu", owner: "Ash")], [F("Schiggy", owner: "Misty")], new BattleRandom(1));

        var lines = result.Log.ToLines(Species);

        Assert.Equal("Ash schickt Pikachu in den Kampf.", lines[1]);
        Assert.Equal("Pikachu (Ash, HP 35) hat Schiggy (Misty, HP 44 → 0) 50 Schaden zugefügt. Sehr effektiv!", lines[3]);
        Assert.Equal("Schiggy (Misty) wurde besiegt.", lines[4]);
    }

    [Fact]
    public void A_target_shows_its_hp_before_and_after_each_hit()
    {
        // The second hit is more than Schiggy has left: the line shows the 30 it had, not only the 0 after (fix/battle-log-hp-before).
        var log = new BattleLog(
            [new LogFighter(0, 25, 70, "1", "Ash"), new LogFighter(1, 7, 88, "2", "Misty")],
            [
                new BattleEvent(BattleEventKind.Attack, 1, 0, 70, 1, 30, 58),
                new BattleEvent(BattleEventKind.Attack, 1, 1, 30, 0, 50, 20),
                new BattleEvent(BattleEventKind.Attack, 2, 0, 50, 1, 0, 45),
                new BattleEvent(BattleEventKind.Fainted, 2, 1),
            ]);

        var lines = log.ToLines(Species);

        Assert.Equal(
            [
                "Runde 1",
                "Pikachu (Ash, HP 70) hat Schiggy (Misty, HP 88 → 30) 58 Schaden zugefügt.",
                "Schiggy (Misty, HP 30) hat Pikachu (Ash, HP 70 → 50) 20 Schaden zugefügt.",
                "Runde 2",
                "Pikachu (Ash, HP 50) hat Schiggy (Misty, HP 30 → 0) 45 Schaden zugefügt.",
                "Schiggy (Misty) wurde besiegt.",
            ],
            lines);
    }

    [Fact]
    public void Each_hit_carries_its_target_with_the_hp_before_and_after()
    {
        // The last hit (45) is more than Schiggy's 30: the hit says 30 → 0, what arrived, for the website's bar (claude/hp-balken-kampf-log-mockup-de3cfe).
        var log = new BattleLog(
            [new LogFighter(0, 25, 70, "1", "Ash"), new LogFighter(1, 7, 88, "2", "Misty")],
            [
                new BattleEvent(BattleEventKind.Attack, 1, 0, 70, 1, 30, 58),
                new BattleEvent(BattleEventKind.Attack, 1, 1, 30, 0, 50, 20),
                new BattleEvent(BattleEventKind.Attack, 2, 0, 50, 1, 0, 45),
                new BattleEvent(BattleEventKind.Fainted, 2, 1),
            ]);

        var text = log.ToText(Species);

        Assert.Equal(
            [null, new TargetHp(1, 88, 30), new TargetHp(0, 70, 50), null, new TargetHp(1, 30, 0), null],
            text.Select(l => l.Hit));
        Assert.Equal([", HP 88 → 30", ", HP 70 → 50", ", HP 30 → 0"], text.SelectMany(l => l.Spans).Where(s => s.IsTargetHp).Select(s => s.Text));
    }

    [Theory]
    [InlineData(2.0, " Sehr effektiv!")]
    [InlineData(1.0, null)]
    public void A_critical_hit_is_a_part_of_its_own(double typeFactor, string? effect)
    {
        var log = new BattleLog(
            [new LogFighter(0, 25, 70, "1", "Ash"), new LogFighter(1, 7, 88, null, "Boss")],
            [new BattleEvent(BattleEventKind.Attack, 1, 0, 70, 1, 38, 50, Critical: true, TypeFactor: typeFactor)]);

        var spans = log.ToText(Species)[1].Spans;

        Assert.Equal(
            [
                new LogSpan("Pikachu", 0, IsSpecies: true), new LogSpan(" (", 0), new LogSpan("Ash", 0, IsOwner: true), new LogSpan(", HP 70)", 0),
                new LogSpan(" hat "),
                new LogSpan("Schiggy", 1, IsSpecies: true), new LogSpan(" (", 1), new LogSpan("Boss", 1, IsOwner: true),
                new LogSpan(", HP 88 → 38", 1, IsTargetHp: true), new LogSpan(")", 1),
                new LogSpan(" 50 Schaden zugefügt."), new LogSpan(" Volltreffer!", IsCritical: true),
                .. effect is null ? Array.Empty<LogSpan>() : [new LogSpan(effect)],
            ],
            spans);
    }
}
