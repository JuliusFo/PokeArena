using PokeArena.Contracts;

namespace PokeArena.Web.Services;

/// <summary>
/// The „Schaden“ box of a fight page (feat/damage-meter): what each side dealt in total, then a ranking — the
/// trainers in a boss fight, every Pokémon of both sides otherwise. Bars are in the trainer's chat colour (the boss
/// and NPC Pokémon in the colour of their first type, like the boss's name in the log), darkened like the names in the
/// light theme; the numbers sit inside the bar in white, like the HP numbers (feat/damage-text-white).
/// </summary>
public static class DamageMeter
{
    /// <param name="Totals">One bar per side, no rank; a side of several colours is <see cref="Bar.Mixed"/>.</param>
    /// <param name="RankingTitle">„Trainer“ in a boss fight, otherwise „Pokémon“.</param>
    public sealed record Board(IReadOnlyList<Bar> Totals, string RankingTitle, IReadOnlyList<Bar> Ranking);

    /// <summary>
    /// The box for <paramref name="battle"/>; null for files written before the damage was stored, and for files no
    /// bot writes (negative values). Sums are <see cref="long"/>: a checked <c>int</c> sum of absurd values would throw
    /// while rendering and take the whole page down (review feat/damage-meter).
    /// </summary>
    public static Board? For(BattlePage battle)
    {
        var used = battle.Sides
            .SelectMany((side, s) => side.Fighters.Select(f => (Fighter: f, Side: s)))
            .Where(x => x.Fighter.Used != false)
            .ToList();
        if (battle.Sides.Count == 0 || used.Count == 0 || used.Any(x => x.Fighter.Damage is null or < 0) ||
            battle.Sides.Any(side => side.Fighters.Any(f => f.MaxHp < 0)))
        {
            return null;
        }

        var boss = battle.Kind == BattleKind.Boss;
        var totals = battle.Sides
            .Select((side, s) =>
            {
                var fighters = used.Where(x => x.Side == s).ToList();
                var colors = fighters.Select(x => Fill(battle, x.Side, x.Fighter)).Distinct().ToList();
                return (Name: side.Name,
                    Detail: boss && s == 0 ? $"({fighters.Count})" : null,
                    Damage: fighters.Sum(x => (long)x.Fighter.Damage!.Value),
                    Fill: colors.Count == 1 ? colors[0] : Bar.Mixed);
            })
            .ToList();
        var bossHp = boss && battle.Sides.Count > 1 ? battle.Sides[1].Fighters.Sum(f => (long)f.MaxHp) : 0;
        var ranked = used
            .Where(x => !boss || x.Side == 0)
            .OrderByDescending(x => x.Fighter.Damage)
            .ToList();
        var damages = ranked.Select(x => x.Fighter.Damage!.Value).ToList();

        return new Board(
            [.. totals.Select(t => Bar.Make(null, t.Name, t.Detail, Bar.Number(t.Damage), t.Damage, totals.Max(x => x.Damage), t.Fill))],
            boss ? "Trainer" : "Pokémon",
            [.. ranked.Select(x =>
            {
                var f = x.Fighter;
                var damage = f.Damage!.Value;
                var rank = $"{1 + damages.Count(d => d > damage)}.";
                var value = bossHp > 0 ? $"{Bar.Number(damage)} · {Bar.Number((long)Math.Round(100.0 * damage / bossHp))} %" : Bar.Number(damage);
                return boss
                    ? Bar.Make(rank, f.Owner, f.Species.Name, value, damage, damages[0], Fill(battle, x.Side, f))
                    : Bar.Make(rank, f.Species.Name, f.Owner, value, damage, damages[0], Fill(battle, x.Side, f));
            })]);
    }

    /// <summary>
    /// The boss and NPC Pokémon in the colour of their first type, a trainer's Pokémon in the trainer's chat colour.
    /// Never tell the boss or an NPC by a missing trainer id — a deleted trainer's fighters have none either.
    /// </summary>
    private static string Fill(BattlePage battle, int side, BattleFighter fighter)
    {
        var color = side == 1 && battle.Kind is BattleKind.Boss or BattleKind.Npc
            ? fighter.Species.Types.Count > 0 ? Tint.TypeColors.GetValueOrDefault(Format.TypeClass(fighter.Species.Types[0])) : null
            : fighter.Color;
        return Tint.IsColor(color) ? color!.ToLowerInvariant() : Bar.Neutral;
    }
}
