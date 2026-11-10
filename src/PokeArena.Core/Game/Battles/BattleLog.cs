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
    /// so is „Volltreffer!“.
    /// </summary>
    public IReadOnlyList<LogText> ToText(SpeciesCatalog species)
    {
        var lines = new List<LogText>();
        var round = 0;
        foreach (var e in Events)
        {
            if (e.Round != round && e.Kind != BattleEventKind.RoundLimit)
            {
                round = e.Round;
                lines.Add(new LogText([new LogSpan($"Runde {round}")], IsRound: true));
            }

            lines.Add(new LogText(e.Kind switch
            {
                BattleEventKind.Attack =>
                [
                    .. Label(species, e.Fighter, e.FighterHp),
                    new(" hat "),
                    .. Label(species, e.Target ?? 0, e.TargetHp),
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
            }));
        }

        return lines;
    }

    /// <summary>„Pikachu (Ash, HP 24)“ in four parts that all name the fighter: species, „ (“, owner, the rest.</summary>
    private LogSpan[] Label(SpeciesCatalog species, int index, int? hp = null)
    {
        var fighter = Fighters[index];
        return
        [
            new(species.Get(fighter.SpeciesId).Name, index, IsSpecies: true),
            new(" (", index),
            new(fighter.Owner, index, IsOwner: true),
            new(hp is { } value ? $", HP {value.ToString(CultureInfo.InvariantCulture)})" : ")", index),
        ];
    }

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
public sealed record LogText(IReadOnlyList<LogSpan> Spans, bool IsRound = false)
{
    public string Text => string.Concat(Spans.Select(s => s.Text));
}

/// <param name="Fighter">Index into <see cref="BattleLog.Fighters"/> if the text belongs to that fighter's label.</param>
/// <param name="IsOwner">The text is the fighter's owner.</param>
/// <param name="IsSpecies">The text is the fighter's species name.</param>
/// <param name="IsCritical">The text is „Volltreffer!“.</param>
public sealed record LogSpan(string Text, int? Fighter = null, bool IsOwner = false, bool IsSpecies = false, bool IsCritical = false);

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
