using System.Globalization;
using PokeArena.Contracts;

namespace PokeArena.Web.Services;

/// <summary>
/// The „Schaden“ box of a fight page (feat/damage-meter): what each side dealt in total, then a ranking — the
/// trainers in a boss fight, every Pokémon of both sides otherwise. Bars are in the trainer's chat colour (the boss
/// and NPC Pokémon in the colour of their first type, like the boss's name in the log), as picked like the names; the
/// numbers sit inside the bar in white, like the HP numbers (feat/damage-text-white).
/// </summary>
public static class DamageMeter
{
    /// <summary>Bar of a fighter without a usable colour: a trainer without a chat colour, a deleted trainer.</summary>
    public const string Neutral = "#6b6b76";

    /// <summary>Bar of a side whose fighters have different colours (the trainers of a boss fight): the HP bar's green.</summary>
    public const string Mixed = "#1b7f45";

    /// <param name="Rank">„1.“; equal damage shares a rank. Null for the totals.</param>
    /// <param name="Name">Bold part: the trainer in a boss fight, otherwise the Pokémon or the side.</param>
    /// <param name="Detail">Plain part after the name: the Pokémon, its owner, or the number of trainers.</param>
    /// <param name="Value">„148“, in a boss fight's ranking with the share of the boss's HP: „148 · 26 %“.</param>
    /// <param name="Share">Bar length in percent of the longest bar of the list, invariant „0.#“.</param>
    /// <param name="Fill">Bar colour, <c>#RRGGBB</c> as picked — the colour of the name in the cards and the log.</param>
    public sealed record Bar(string? Rank, string Name, string? Detail, string Value, string Share, string Fill);

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
                    Fill: colors.Count == 1 ? colors[0] : Mixed);
            })
            .ToList();
        var bossHp = boss && battle.Sides.Count > 1 ? battle.Sides[1].Fighters.Sum(f => (long)f.MaxHp) : 0;
        var ranked = used
            .Where(x => !boss || x.Side == 0)
            .OrderByDescending(x => x.Fighter.Damage)
            .ToList();
        var damages = ranked.Select(x => x.Fighter.Damage!.Value).ToList();

        return new Board(
            [.. totals.Select(t => Make(null, t.Name, t.Detail, Number(t.Damage), t.Damage, totals.Max(x => x.Damage), t.Fill))],
            boss ? "Trainer" : "Pokémon",
            [.. ranked.Select(x =>
            {
                var f = x.Fighter;
                var damage = f.Damage!.Value;
                var rank = $"{1 + damages.Count(d => d > damage)}.";
                var value = bossHp > 0 ? $"{Number(damage)} · {Number((long)Math.Round(100.0 * damage / bossHp))} %" : Number(damage);
                return boss
                    ? Make(rank, f.Owner, f.Species.Name, value, damage, damages[0], Fill(battle, x.Side, f))
                    : Make(rank, f.Species.Name, f.Owner, value, damage, damages[0], Fill(battle, x.Side, f));
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
        return Tint.IsColor(color) ? color!.ToLowerInvariant() : Neutral;
    }

    private static Bar Make(string? rank, string name, string? detail, string value, long damage, long longest, string fill) =>
        new(rank, name, detail, value,
            (longest > 0 ? 100.0 * damage / longest : 0).ToString("0.#", CultureInfo.InvariantCulture),
            fill);

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
