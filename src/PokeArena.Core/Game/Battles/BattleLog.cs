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

    /// <summary>MaxRunden reached; the side with the higher remaining HP sum wins (spec §5).</summary>
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

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <exception cref="JsonException">Not a battle log.</exception>
    public static BattleLog FromJson(string json) =>
        JsonSerializer.Deserialize<BattleLog>(json, Json) ?? throw new JsonException("Empty battle log");

    /// <summary>German log lines (spec §5), with a „Runde N“ header whenever the round changes.</summary>
    public IReadOnlyList<string> ToLines(SpeciesCatalog species)
    {
        var lines = new List<string>();
        var round = 0;
        foreach (var e in Events)
        {
            if (e.Round != round && e.Kind != BattleEventKind.RoundLimit)
            {
                round = e.Round;
                lines.Add($"Runde {round}");
            }

            lines.Add(e.Kind switch
            {
                BattleEventKind.Attack =>
                    $"{Label(species, e.Fighter, e.FighterHp)} hat {Label(species, e.Target ?? 0, e.TargetHp)} {e.Damage} Schaden zugefügt." +
                    (e.Critical ? " Volltreffer!" : "") +
                    (e.TypeFactor > 1.0 ? " Sehr effektiv!" : e.TypeFactor < 1.0 ? " Nicht sehr effektiv." : ""),
                BattleEventKind.Fainted => $"{Label(species, e.Fighter)} wurde besiegt.",
                BattleEventKind.SentOut => $"{Fighters[e.Fighter].Owner} schickt {species.Get(Fighters[e.Fighter].SpeciesId).Name} in den Kampf.",
                BattleEventKind.RoundLimit => "Rundenlimit erreicht – die Seite mit mehr verbleibenden HP gewinnt.",
                _ => throw new InvalidOperationException($"Unknown event {e.Kind}"),
            });
        }

        return lines;
    }

    private string Label(SpeciesCatalog species, int index, int? hp = null)
    {
        var fighter = Fighters[index];
        var name = species.Get(fighter.SpeciesId).Name;
        return hp is { } value
            ? $"{name} ({fighter.Owner}, HP {value.ToString(CultureInfo.InvariantCulture)})"
            : $"{name} ({fighter.Owner})";
    }
}

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
