namespace PokeArena.Core.Game.Battles;

/// <param name="Survived">Per participant, in input order: still standing at the end.</param>
/// <param name="HitRoundLimit">MaxRounds reached — a bug; the caller logs a warning.</param>
/// <param name="BossHp">
/// The boss's HP left at the end: 0 if it fainted; can be above 0 with <paramref name="BossDefeated"/> when the round
/// limit decided.
/// </param>
/// <param name="BossMaxHp">The boss's HP at the start.</param>
public sealed record BossBattleResult(
    bool BossDefeated, IReadOnlyList<bool> Survived, int Rounds, bool HitRoundLimit, BattleLog Log, int BossHp, int BossMaxHp);

/// <summary>
/// The group fight against a boss (spec §4). Per round all living participants hit the boss once in random order;
/// then the boss hits <see cref="TargetsInRound"/> distinct random living participants (all, if fewer are left) —
/// N / BossTargetDivisor on average, N being the participant count at the start. A fainted participant stays out.
/// </summary>
public sealed class BossBattle(DamageCalculator damage)
{
    /// <param name="boss">Already scaled (<see cref="Fighter.Create"/> with the boss HP multiplier).</param>
    /// <param name="party">One fighter per participant; the log keeps this order.</param>
    public BossBattleResult Fight(Fighter boss, IReadOnlyList<Fighter> party, int targetDivisor, BattleRandom random)
    {
        ArgumentOutOfRangeException.ThrowIfZero(party.Count);
        ArgumentOutOfRangeException.ThrowIfLessThan(targetDivisor, 1);

        var recorder = new BattleRecorder([.. party.Select(p => (p, 0)), (boss, 1)]);
        var bossIndex = party.Count;
        var maxRounds = damage.Settings.MaxRounds;

        for (var round = 1; round <= maxRounds; round++)
        {
            foreach (var attacker in random.Shuffled(Living(recorder, party.Count)))
            {
                if (recorder.Attack(round, attacker, bossIndex, damage, random))
                {
                    return Result(recorder, party.Count, bossDefeated: true, round, hitRoundLimit: false);
                }
            }

            var targets = TargetsInRound(party.Count, targetDivisor, round);
            foreach (var target in random.Shuffled(Living(recorder, party.Count)).Take(targets))
            {
                recorder.Attack(round, bossIndex, target, damage, random);
            }

            if (!Living(recorder, party.Count).Any())
            {
                return Result(recorder, party.Count, bossDefeated: false, round, hitRoundLimit: false);
            }
        }

        // Safety net (spec §5): the higher remaining HP sum wins; on a tie the boss escapes.
        recorder.Add(new BattleEvent(BattleEventKind.RoundLimit, maxRounds));
        var partyHp = Living(recorder, party.Count).Sum(i => recorder.Hp[i]);
        return Result(recorder, party.Count, partyHp > recorder.Hp[bossIndex], maxRounds, hitRoundLimit: true);
    }

    /// <summary>
    /// How many participants the boss hits in <paramref name="round"/> (1-based): N / d per round on average, the
    /// remainder carried over and rounded up first, ⌈r·N/d⌉ − ⌈(r−1)·N/d⌉. With N = 4 and d = 3 that is 2, 1, 1, 2,
    /// 1, 1 …; a whole multiple of d hits N / d every round. Until 1.7 the boss hit ⌈N / d⌉ every round, so groups of
    /// 2, 4, 5 and 7 lost more often than 3 and 6 (ADR-0006 addendum 2026-11-19).
    /// </summary>
    public static int TargetsInRound(int participants, int targetDivisor, int round)
    {
        int CeilDiv(int value) => (value + targetDivisor - 1) / targetDivisor;
        return CeilDiv(round * participants) - CeilDiv((round - 1) * participants);
    }

    private static IEnumerable<int> Living(BattleRecorder recorder, int partySize) =>
        Enumerable.Range(0, partySize).Where(recorder.IsAlive);

    /// <remarks>The boss is the last fighter of the recorder, right after the party.</remarks>
    private static BossBattleResult Result(BattleRecorder recorder, int partySize, bool bossDefeated, int rounds, bool hitRoundLimit) =>
        new(
            bossDefeated,
            [.. Enumerable.Range(0, partySize).Select(recorder.IsAlive)],
            rounds,
            hitRoundLimit,
            recorder.ToLog(),
            recorder.Hp[partySize],
            recorder.Fighters[partySize].MaxHp);
}
