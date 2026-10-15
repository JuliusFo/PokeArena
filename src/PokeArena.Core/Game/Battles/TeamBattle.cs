namespace PokeArena.Core.Game.Battles;

/// <param name="Attacks">Number of hits, for balancing statistics.</param>
/// <param name="HitRoundLimit">MaxRunden reached — a bug; the caller logs a warning.</param>
public sealed record TeamBattleResult(bool ChallengerWon, int Attacks, bool HitRoundLimit, BattleLog Log);

/// <summary>
/// One-vs-one with teams (spec §5), used by PvP and NPC fights and the balancing simulator: one Pokemon per side is
/// active, the challenger strikes first, then the sides alternate. When a Pokemon faints its side sends the next
/// team member, and that side attacks first.
/// </summary>
public sealed class TeamBattle(DamageCalculator damage)
{
    /// <param name="challenger">Team in send order (the caller draws it randomly).</param>
    /// <param name="opponent">Team in send order.</param>
    public TeamBattleResult Fight(IReadOnlyList<Fighter> challenger, IReadOnlyList<Fighter> opponent, BattleRandom random)
    {
        ArgumentOutOfRangeException.ThrowIfZero(challenger.Count);
        ArgumentOutOfRangeException.ThrowIfZero(opponent.Count);

        var recorder = new BattleRecorder([.. challenger.Select(f => (f, 0)), .. opponent.Select(f => (f, 1))]);
        int[] firstIndex = [0, challenger.Count];
        int[] teamSize = [challenger.Count, opponent.Count];
        int[] active = [0, 0];
        var attackerSide = 0;

        // A round is one hit per side; MaxRunden caps the hits at twice that.
        var maxAttacks = damage.Settings.MaxRounds * 2;

        recorder.Add(new BattleEvent(BattleEventKind.SentOut, 1, firstIndex[0]));
        recorder.Add(new BattleEvent(BattleEventKind.SentOut, 1, firstIndex[1]));

        while (recorder.AttackCount < maxAttacks)
        {
            var defenderSide = 1 - attackerSide;
            var round = recorder.AttackCount / 2 + 1;
            var attacker = firstIndex[attackerSide] + active[attackerSide];
            var defender = firstIndex[defenderSide] + active[defenderSide];

            if (!recorder.Attack(round, attacker, defender, damage, random))
            {
                attackerSide = defenderSide;
                continue;
            }

            active[defenderSide]++;
            if (active[defenderSide] == teamSize[defenderSide])
            {
                return new TeamBattleResult(attackerSide == 0, recorder.AttackCount, false, recorder.ToLog());
            }

            recorder.Add(new BattleEvent(BattleEventKind.SentOut, round, firstIndex[defenderSide] + active[defenderSide]));
            attackerSide = defenderSide;
        }

        // Safety net (spec §5): the higher remaining HP sum wins; on a tie the challenger loses.
        recorder.Add(new BattleEvent(BattleEventKind.RoundLimit, damage.Settings.MaxRounds));
        var challengerHp = recorder.Hp.Take(challenger.Count).Sum();
        var opponentHp = recorder.Hp.Skip(challenger.Count).Sum();
        return new TeamBattleResult(challengerHp > opponentHp, recorder.AttackCount, true, recorder.ToLog());
    }
}
