namespace PokeArena.Core.Data;

/// <summary>A registered viewer. Identified only by the Twitch user id, never by name (spec §1).</summary>
public sealed class Trainer
{
    public required string TwitchUserId { get; init; }

    /// <summary>Current Twitch display name; refreshed when the trainer is seen in chat.</summary>
    public required string DisplayName { get; set; }

    public DateTimeOffset RegisteredAt { get; init; }

    public List<Specimen> Specimens { get; init; } = [];
}

public enum SpecimenOrigin
{
    Starter,

    /// <summary>Caught by surviving a boss fight (spec §4).</summary>
    Boss,
}

/// <summary>One owned Pokemon. Duplicates of a species are separate specimens (spec §1).</summary>
public sealed class Specimen
{
    public long Id { get; init; }

    public required string TrainerId { get; set; }

    public Trainer? Trainer { get; set; }

    /// <summary>Id in <see cref="SpeciesCatalog"/>; base values stay in the JSON (ADR-0001).</summary>
    public int SpeciesId { get; init; }

    public DateTimeOffset CaughtAt { get; init; }

    public SpecimenOrigin Origin { get; init; }

    /// <summary>The protected starter: exactly one per trainer, never transferred (spec §1, §6).</summary>
    public bool IsStarter { get; init; }
}

public enum BattleKind
{
    Boss,
}

public enum BattleOutcome
{
    BossDefeated,
    BossEscaped,
}

/// <summary>One resolved fight with its seed and full log (ADR-0004). Logs are data, never rebuilt from the seed.</summary>
public sealed class Battle
{
    public long Id { get; init; }

    public BattleKind Kind { get; init; }

    public DateTimeOffset FoughtAt { get; init; }

    public int Seed { get; init; }

    /// <summary>Boss fights: the boss species.</summary>
    public int? BossSpeciesId { get; init; }

    public BattleOutcome Outcome { get; init; }

    public int Rounds { get; init; }

    /// <summary>The <c>BattleLog</c> as JSON, stored verbatim.</summary>
    public required string LogJson { get; init; }

    public List<BattleParticipant> Participants { get; init; } = [];
}

/// <summary>A trainer who took part in a <see cref="Battle"/>; what they fielded is in the log.</summary>
public sealed class BattleParticipant
{
    public long Id { get; init; }

    public long BattleId { get; init; }

    public required string TrainerId { get; init; }

    public Trainer? Trainer { get; set; }

    /// <summary>Boss fights: survived and caught the boss.</summary>
    public bool Won { get; init; }
}

/// <summary>An achievement reached by a trainer (spec §1). Permanent: never revoked, only deleted with the trainer.</summary>
public sealed class AchievementUnlock
{
    public required string TrainerId { get; init; }

    public Trainer? Trainer { get; set; }

    /// <summary>Distinct species owned when unlocked; the achievement's number („AV 15“).</summary>
    public int Threshold { get; init; }

    public DateTimeOffset UnlockedAt { get; init; }
}

/// <summary>A cooldown that must survive a restart, e.g. the global boss cooldown.</summary>
public sealed class Cooldown
{
    public required string Key { get; init; }

    public DateTimeOffset Until { get; set; }
}
