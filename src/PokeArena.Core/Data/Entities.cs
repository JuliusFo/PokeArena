namespace PokeArena.Core.Data;

/// <summary>A registered viewer. Identified only by the Twitch user id, never by name (spec §1).</summary>
public sealed class Trainer
{
    public required string TwitchUserId { get; init; }

    /// <summary>Current Twitch display name; refreshed when the trainer is seen in chat.</summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Current Twitch login (lower case), refreshed together with the display name. Chat mentions use it
    /// (<c>@login</c>); for localized display names it is the only name that can be typed.
    /// </summary>
    public required string Login { get; set; }

    public DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Token balance: the first win against an NPC adds its tokens (spec §7); spending them comes later.</summary>
    public int Tokens { get; set; }

    public List<Specimen> Specimens { get; init; } = [];
}

public enum SpecimenOrigin
{
    Starter,

    /// <summary>Caught by surviving a boss fight (spec §4).</summary>
    Boss,

    /// <summary>Reward for the first win against an NPC (spec §7).</summary>
    Npc,
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

    /// <summary>
    /// When the current owner got it: <see cref="CaughtAt"/>, or the fight in which PvP moved it to them. Breaks
    /// leaderboard ties (spec §9).
    /// </summary>
    public DateTimeOffset AcquiredAt { get; set; }

    /// <summary>How the specimen was first obtained; like <see cref="CaughtAt"/> it stays when PvP moves it to another trainer.</summary>
    public SpecimenOrigin Origin { get; init; }

    /// <summary>The protected starter: exactly one per trainer, never transferred (spec §1, §6).</summary>
    public bool IsStarter { get; init; }
}

public enum BattleKind
{
    Boss,

    /// <summary>Trainer against trainer (spec §6).</summary>
    Pvp,

    /// <summary>Trainer against an NPC (spec §7).</summary>
    Npc,
}

public enum BattleOutcome
{
    BossDefeated,
    BossEscaped,

    /// <summary>PvP: the trainer who issued the challenge won. NPC: the trainer won.</summary>
    ChallengerWon,

    /// <summary>PvP: the challenged trainer won. NPC: the NPC won.</summary>
    OpponentWon,
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

    /// <summary>NPC fights: id in <see cref="NpcCatalog"/>.</summary>
    public string? NpcId { get; init; }

    /// <summary>
    /// PvP: the specimen the winner took from the loser. NPC: the reward specimen of a first win. No foreign
    /// key — the battle outlives it.
    /// </summary>
    public long? LootSpecimenId { get; init; }

    /// <summary>Species of the loot, so the battle stays readable after the specimen is gone.</summary>
    public int? LootSpeciesId { get; init; }

    /// <summary>NPC first wins: tokens credited to the trainer.</summary>
    public int? Tokens { get; init; }

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

    /// <summary>Boss fights: survived and caught the boss. PvP and NPC: won the fight.</summary>
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

/// <summary>A trainer's badge of an NPC: earned by the first win against it (spec §7), never revoked.</summary>
public sealed class NpcBadge
{
    public required string TrainerId { get; init; }

    public Trainer? Trainer { get; set; }

    /// <summary>Id in <see cref="NpcCatalog"/>.</summary>
    public required string NpcId { get; init; }

    public DateTimeOffset WonAt { get; init; }

    /// <summary>The winning battle. No foreign key, like the loot columns of <see cref="Battle"/>.</summary>
    public long BattleId { get; init; }
}

/// <summary>
/// Outbox of the website (spec §10): a site file that has to be rendered from the database and uploaded. Game
/// logic adds rows in its own transaction; the publish worker renders the file when it uploads, so a row only
/// names the path, and queuing the same path twice is one upload.
/// </summary>
public sealed class PublishQueueItem
{
    /// <summary>Object key, see <c>PokeArena.Contracts.SitePaths</c>.</summary>
    public required string Path { get; init; }

    /// <summary>Raised on every enqueue; the worker removes the row only if nobody queued it again meanwhile.</summary>
    public int Revision { get; set; }

    public DateTimeOffset QueuedAt { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>Failed uploads since the last enqueue; drives the backoff.</summary>
    public int Attempts { get; set; }

    public string? LastError { get; set; }
}

/// <summary>A cooldown that must survive a restart: the global boss cooldown, one per trainer and NPC.</summary>
public sealed class Cooldown
{
    public required string Key { get; init; }

    public DateTimeOffset Until { get; set; }
}
