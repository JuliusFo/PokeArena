using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PokeArena.Core.Data;

namespace PokeArena.Core.Game.Battles;

public enum BattleEventKind
{
    /// <summary><see cref="BattleEvent.Fighter"/> hits <see cref="BattleEvent.Target"/>.</summary>
    Attack,

    /// <summary><see cref="BattleEvent.Fighter"/> fainted.</summary>
    Fainted,

    /// <summary><see cref="BattleEvent.Fighter"/> enters the fight for its side (team fights).</summary>
    SentOut,

    /// <summary>MaxRounds reached; the side with the higher remaining HP sum wins (spec §5).</summary>
    RoundLimit,
}

/// <summary>One step of a fight. Fighters are indices into <see cref="BattleLog.Fighters"/>; HP values are after the step.</summary>
public sealed record BattleEvent(
    BattleEventKind Kind,
    int Round,
    int Fighter = 0,
    int FighterHp = 0,
    int? Target = null,
    int TargetHp = 0,
    int Damage = 0,
    bool Critical = false,
    double TypeFactor = 1.0);

/// <param name="Side">0 = participants or challenger, 1 = boss, NPC or opponent.</param>
public sealed record LogFighter(int Side, int SpeciesId, int MaxHp, string? TrainerId, string Owner);

/// <summary>
/// The full course of a fight as data (ADR-0004): stored verbatim with the battle, never rebuilt from the seed.
/// Text for chat, UI and website is rendered from it.
/// </summary>
public sealed record BattleLog(IReadOnlyList<LogFighter> Fighters, IReadOnlyList<BattleEvent> Events)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Owner shown for the fighters of a deleted trainer (spec §11).</summary>
    public const string DeletedOwner = "[gelöscht]";

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>The same fight with the trainer's name and id removed; everything else stays as it happened.</summary>
    public BattleLog WithoutTrainer(string trainerId) => this with
    {
        Fighters = [.. Fighters.Select(f => f.TrainerId == trainerId ? f with { TrainerId = null, Owner = DeletedOwner } : f)],
    };

    /// <summary>Indices of the fighters that never fainted.</summary>
    public IEnumerable<int> Survivors()
    {
        var fainted = Events.Where(e => e.Kind == BattleEventKind.Fainted).Select(e => e.Fighter).ToHashSet();
        return Enumerable.Range(0, Fighters.Count).Where(i => !fainted.Contains(i));
    }

    /// <summary>HP of fighter <paramref name="index"/> after its last hit taken; its full HP if it was never hit.</summary>
    public int HpLeft(int index) =>
        Events.LastOrDefault(e => e.Kind == BattleEventKind.Attack && e.Target == index) is { } lastHit
            ? lastHit.TargetHp
            : Fighters[index].MaxHp;

    /// <summary>
    /// Damage each fighter dealt, indexed like <see cref="Fighters"/>: only what arrived, so a last hit counts the
    /// target's HP left before it, not the full <see cref="BattleEvent.Damage"/>. Everything one side dealt therefore
    /// adds up to the HP the other side lost (feat/damage-meter).
    /// </summary>
    public IReadOnlyList<int> DamageDealt()
    {
        var dealt = new int[Fighters.Count];
        foreach (var (e, targetHpBefore) in WithTargetHpBefore())
        {
            if (e is { Kind: BattleEventKind.Attack, Target: not null })
            {
                dealt[e.Fighter] += targetHpBefore - e.TargetHp;
            }
        }

        return dealt;
    }

    /// <summary>
    /// Every event with its target's HP just before it (0 for events without a target). The one HP replay that
    /// <see cref="DamageDealt"/> and <see cref="ToText"/> share, so the „Schaden“ box and the log's „HP 10 → 0“ never
    /// disagree (review fix/battle-log-hp-before).
    /// </summary>
    private IEnumerable<(BattleEvent Event, int TargetHpBefore)> WithTargetHpBefore()
    {
        var hp = Fighters.Select(f => f.MaxHp).ToArray();
        foreach (var e in Events)
        {
            if (e is { Kind: BattleEventKind.Attack, Target: { } target })
            {
                yield return (e, hp[target]);
                hp[target] = e.TargetHp;
            }
            else
            {
                yield return (e, 0);
            }
        }
    }

    /// <summary>
    /// Whether fighter <paramref name="index"/> took part: in a team fight only once sent in; in a boss fight (no
    /// send-in events) everyone fights from the start.
    /// </summary>
    public bool WasUsed(int index) =>
        !Events.Any(e => e.Kind == BattleEventKind.SentOut) ||
        Events.Any(e => e.Kind == BattleEventKind.SentOut && e.Fighter == index);

    /// <exception cref="JsonException">Not a battle log.</exception>
    public static BattleLog FromJson(string json) =>
        JsonSerializer.Deserialize<BattleLog>(json, Json) ?? throw new JsonException("Empty battle log");

    /// <summary>German log lines (spec §5), with a „Runde N“ header whenever the round changes.</summary>
    public IReadOnlyList<string> ToLines(SpeciesCatalog species) => [.. ToText(species).Select(line => line.Text)];

    /// <summary>
    /// The lines of <see cref="ToLines"/> in parts, so the website can colour them: a fighter's label „Pikachu (Ash,
    /// HP 24)“ names the fighter, and its species and its owner („Ash“, „Boss“, an NPC's name) are parts of their own;
    /// so is „Volltreffer!“. A hit's target shows its HP before and after the hit („Digda (Ash, HP 10 → 0)“), so a
    /// knockout does not read as a hit on a fighter that already had 0 HP (fix/battle-log-hp-before).
    /// </summary>
    public IReadOnlyList<LogText> ToText(SpeciesCatalog species)
    {
        var lines = new List<LogText>();
        var round = 0;
        foreach (var (e, targetHpBefore) in WithTargetHpBefore())
        {
            if (e.Round != round && e.Kind != BattleEventKind.RoundLimit)
            {
                round = e.Round;
                lines.Add(new LogText([new LogSpan($"Runde {round}")], IsRound: true));
            }

            // An attack always has a target; fighter 0 only keeps a malformed log readable (as before), without a bar.
            var hit = e is { Kind: BattleEventKind.Attack, Target: { } target } ? new HitTarget(target, targetHpBefore, e.TargetHp) : null;
            lines.Add(new LogText(e.Kind switch
            {
                BattleEventKind.Attack =>
                [
                    .. Label(species, e.Fighter, Number(e.FighterHp)),
                    new(" hat "),
                    .. TargetLabel(species, e.Target ?? 0, targetHpBefore, e.TargetHp),
                    .. Outcome(e),
                ],
                BattleEventKind.Fainted => [.. Label(species, e.Fighter), new(" wurde besiegt.")],
                BattleEventKind.SentOut =>
                [
                    new(Fighters[e.Fighter].Owner, e.Fighter, IsOwner: true),
                    new($" schickt {species.Get(Fighters[e.Fighter].SpeciesId).Name} in den Kampf."),
                ],
                BattleEventKind.RoundLimit => [new("Rundenlimit erreicht – die Seite mit mehr verbleibenden HP gewinnt.")],
                _ => throw new InvalidOperationException($"Unknown event {e.Kind}"),
            }, Hit: hit));
        }

        return lines;
    }

    /// <summary>„Pikachu (Ash, HP 24)“ in four parts that all name the fighter: species, „ (“, owner, the rest.</summary>
    private LogSpan[] Label(SpeciesCatalog species, int index, string? hp = null)
    {
        var fighter = Fighters[index];
        return
        [
            new(species.Get(fighter.SpeciesId).Name, index, IsSpecies: true),
            new(" (", index),
            new(fighter.Owner, index, IsOwner: true),
            new(hp is null ? ")" : $", HP {hp})", index),
        ];
    }

    /// <summary>
    /// A hit's target „Schiggy (Misty, HP 88 → 30)“: like <see cref="Label"/>, but the HP are a part of their own
    /// („, HP 88 → 30“, then „)“), so the website can leave them out where it draws them as a bar
    /// (claude/hp-balken-kampf-log-mockup-de3cfe).
    /// </summary>
    private LogSpan[] TargetLabel(SpeciesCatalog species, int index, int before, int after)
    {
        var fighter = Fighters[index];
        return
        [
            new(species.Get(fighter.SpeciesId).Name, index, IsSpecies: true),
            new(" (", index),
            new(fighter.Owner, index, IsOwner: true),
            new($", HP {Number(before)} → {Number(after)}", index, IsTargetHp: true),
            new(")", index),
        ];
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>„ 20 Schaden zugefügt. Volltreffer! Sehr effektiv!“ — „Volltreffer!“ as a part of its own, so it can stand out.</summary>
    private static LogSpan[] Outcome(BattleEvent e)
    {
        var damage = $" {e.Damage.ToString(CultureInfo.InvariantCulture)} Schaden zugefügt.";
        var effect = e.TypeFactor > 1.0 ? " Sehr effektiv!" : e.TypeFactor < 1.0 ? " Nicht sehr effektiv." : "";
        if (!e.Critical)
        {
            return [new(damage + effect)];
        }

        return effect.Length > 0
            ? [new(damage), new(" Volltreffer!", IsCritical: true), new(effect)]
            : [new(damage), new(" Volltreffer!", IsCritical: true)];
    }
}

/// <summary>One rendered log line in parts (see <see cref="BattleLog.ToText"/>).</summary>
/// <param name="IsRound">A „Runde N“ heading.</param>
/// <param name="Hit">For a hit: its target and the target's HP before and after it.</param>
public sealed record LogText(IReadOnlyList<LogSpan> Spans, bool IsRound = false, HitTarget? Hit = null)
{
    public string Text => string.Concat(Spans.Select(s => s.Text));
}

/// <summary>A hit's target (index into <see cref="BattleLog.Fighters"/>) with its HP just before and after the hit.</summary>
public sealed record HitTarget(int Target, int Before, int After);

/// <param name="Fighter">Index into <see cref="BattleLog.Fighters"/> if the text belongs to that fighter's label.</param>
/// <param name="IsOwner">The text is the fighter's owner.</param>
/// <param name="IsSpecies">The text is the fighter's species name.</param>
/// <param name="IsCritical">The text is „Volltreffer!“.</param>
/// <param name="IsTargetHp">The text is a hit target's HP („, HP 88 → 30“).</param>
public sealed record LogSpan(
    string Text, int? Fighter = null, bool IsOwner = false, bool IsSpecies = false, bool IsCritical = false, bool IsTargetHp = false);

/// <summary>Records events and tracks HP while an engine runs a fight.</summary>
internal sealed class BattleRecorder
{
    private readonly List<BattleEvent> _events = [];

    public BattleRecorder(IReadOnlyList<(Fighter Fighter, int Side)> fighters)
    {
        Fighters = [.. fighters.Select(f => f.Fighter)];
        Hp = [.. Fighters.Select(f => f.MaxHp)];
        LogFighters = [.. fighters.Select(f => new LogFighter(f.Side, f.Fighter.Species.Id, f.Fighter.MaxHp, f.Fighter.TrainerId, f.Fighter.Owner))];
    }

    public IReadOnlyList<Fighter> Fighters { get; }

    public int[] Hp { get; }

    public IReadOnlyList<LogFighter> LogFighters { get; }

    public bool IsAlive(int index) => Hp[index] > 0;

    /// <summary>Applies a hit and logs it, plus the faint if the target drops to 0.</summary>
    /// <returns>Whether the target fainted.</returns>
    public bool Attack(int round, int attacker, int target, DamageCalculator damage, BattleRandom random)
    {
        var hit = damage.Hit(Fighters[attacker], Fighters[target], random);
        AttackCount++;
        Hp[target] = Math.Max(0, Hp[target] - hit.Damage);
        _events.Add(new BattleEvent(BattleEventKind.Attack, round, attacker, Hp[attacker], target, Hp[target], hit.Damage, hit.Critical, hit.TypeFactor));
        if (Hp[target] > 0)
        {
            return false;
        }

        _events.Add(new BattleEvent(BattleEventKind.Fainted, round, target));
        return true;
    }

    public void Add(BattleEvent e) => _events.Add(e);

    public int AttackCount { get; private set; }

    public BattleLog ToLog() => new(LogFighters, [.. _events]);
}
