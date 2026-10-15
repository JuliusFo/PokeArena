namespace PokeArena.Core.Game.Battles;

/// <param name="Survived">Per participant, in input order: still standing at the end.</param>
/// <param name="HitRoundLimit">MaxRunden reached — a bug; the caller logs a warning.</param>
public sealed record BossBattleResult(bool BossDefeated, IReadOnlyList<bool> Survived, int Rounds, bool HitRoundLimit, BattleLog Log);

/// <summary>
/// The group fight against a boss (spec §4). Per round all living participants hit the boss once in random order;
/// then the boss hits ⌈N / BossZieleTeiler⌉ distinct random living participants (all, if fewer are left). N is the
/// participant count at the start. A fainted participant stays out.
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
        var targetsPerRound = (party.Count + targetDivisor - 1) / targetDivisor;
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

            foreach (var target in random.Shuffled(Living(recorder, party.Count)).Take(targetsPerRound))
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

    private static IEnumerable<int> Living(BattleRecorder recorder, int partySize) =>
        Enumerable.Range(0, partySize).Where(recorder.IsAlive);

    private static BossBattleResult Result(BattleRecorder recorder, int partySize, bool bossDefeated, int rounds, bool hitRoundLimit) =>
        new(bossDefeated, [.. Enumerable.Range(0, partySize).Select(recorder.IsAlive)], rounds, hitRoundLimit, recorder.ToLog());
}
