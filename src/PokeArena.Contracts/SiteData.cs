namespace PokeArena.Contracts;

/// <summary><c>meta.json</c>: the data format the files were written in.</summary>
public sealed record SiteMeta(int FormatVersion, DateTimeOffset UpdatedAt)
{
    /// <summary>Bump on every breaking change to a record in this project (ADR-0009).</summary>
    public const int CurrentFormatVersion = 1;
}

/// <summary>A species as the site shows it: text only, no images (ADR-0008).</summary>
/// <param name="Types">German type names, primary type first.</param>
public sealed record SpeciesInfo(int Id, string Name, IReadOnlyList<string> Types);

/// <summary><c>rangliste.json</c>: every trainer, best first (spec §9).</summary>
public sealed record Leaderboard(DateTimeOffset UpdatedAt, int PlayerCount, IReadOnlyList<LeaderboardEntry> Entries);

/// <param name="Distinct">Distinct species owned — the ranking value.</param>
/// <param name="Total">All specimens owned, duplicates included.</param>
public sealed record LeaderboardEntry(int Rank, string TrainerId, string DisplayName, int Distinct, int Total);

/// <summary>The kind of a fight — in the site JSON and, stored as text, in the bot's database (review 1.0.3, B2).</summary>
public enum BattleKind
{
    Boss,

    /// <summary>Trainer against trainer (spec §6).</summary>
    Pvp,

    /// <summary>Trainer against an NPC (spec §7).</summary>
    Npc,
}

/// <summary>One fight in a list: German headline and result, rendered by the bot.</summary>
public sealed record BattleSummary(long Id, BattleKind Kind, DateTimeOffset FoughtAt, string Headline, string Result);

/// <summary><c>kaempfe.json</c>: the latest fights, newest first.</summary>
public sealed record BattleIndex(DateTimeOffset UpdatedAt, IReadOnlyList<BattleSummary> Battles);

/// <summary><c>kampf/&lt;id&gt;.json</c>: one fight with its full log. Never changes once written.</summary>
/// <param name="Sides">The trainers' side first, then the boss, NPC or challenged trainer.</param>
/// <param name="Lines">German log lines (spec §5).</param>
public sealed record BattlePage(
    long Id,
    BattleKind Kind,
    DateTimeOffset FoughtAt,
    int Seed,
    string Headline,
    string Result,
    IReadOnlyList<BattleSide> Sides,
    IReadOnlyList<string> Lines);

public sealed record BattleSide(string Name, bool Won, IReadOnlyList<BattleFighter> Fighters);

/// <param name="TrainerId">Owner's Twitch user id; null for bosses and NPCs.</param>
public sealed record BattleFighter(SpeciesInfo Species, string Owner, string? TrainerId, int MaxHp);

/// <summary><c>trainer/&lt;twitch-id&gt;.json</c>: one trainer's collection and history.</summary>
public sealed record TrainerPage(
    string TrainerId,
    string DisplayName,
    DateTimeOffset RegisteredAt,
    DateTimeOffset UpdatedAt,
    int Distinct,
    int Total,
    int Tokens,
    int Battles,
    int Wins,
    IReadOnlyList<PokedexEntry> Pokedex,
    IReadOnlyList<AchievementInfo> Achievements,
    IReadOnlyList<BadgeInfo> Badges,
    IReadOnlyList<BattleSummary> RecentBattles);

/// <param name="HasStarter">One of the specimens is the protected starter.</param>
public sealed record PokedexEntry(SpeciesInfo Species, int Count, bool HasStarter);

/// <param name="Threshold">Distinct species needed („AV 15“).</param>
public sealed record AchievementInfo(int Threshold, string NpcName, DateTimeOffset UnlockedAt);

public sealed record BadgeInfo(string NpcName, DateTimeOffset WonAt, long BattleId);

/// <summary>What the local OBS overlay shows: the latest fight and the top of the leaderboard.</summary>
public sealed record OverlayState(BattleSummary? LatestBattle, IReadOnlyList<LeaderboardEntry> Top);
