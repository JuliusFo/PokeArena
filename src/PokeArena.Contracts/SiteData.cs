using System.Text.Json.Serialization;

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

/// <summary>
/// <c>kampf/&lt;id&gt;.json</c>: one fight with its full log. The fight itself never changes; the file is rewritten only
/// when a trainer in it is deleted or on „Alles neu veröffentlichen“.
/// </summary>
/// <param name="Sides">The trainers' side first, then the boss, NPC or challenged trainer.</param>
/// <param name="Lines">German log lines (spec §5) as plain text.</param>
/// <param name="Log">The same lines in parts that name their fighter, so the site can colour them; null in older files.</param>
public sealed record BattlePage(
    long Id,
    BattleKind Kind,
    DateTimeOffset FoughtAt,
    int Seed,
    string Headline,
    string Result,
    IReadOnlyList<BattleSide> Sides,
    IReadOnlyList<string> Lines,
    IReadOnlyList<LogLine>? Log = null);

public sealed record BattleSide(string Name, bool Won, IReadOnlyList<BattleFighter> Fighters);

/// <param name="TrainerId">Owner's Twitch user id; null for bosses and NPCs.</param>
/// <param name="HpLeft">HP at the end of the fight (0 = fainted); null in files written before it existed.</param>
/// <param name="Used">False if a team fight ended before this one was sent in; null in older files.</param>
/// <param name="Color">The owner's Twitch chat colour (<c>#RRGGBB</c>) as picked, not adjusted for contrast; null if unknown.</param>
/// <param name="Damage">Damage this fighter dealt, counting only what arrived (a last hit up to the target's HP left);
/// null in files written before it existed.</param>
public sealed record BattleFighter(
    SpeciesInfo Species,
    string Owner,
    string? TrainerId,
    int MaxHp,
    int? HpLeft = null,
    bool? Used = null,
    string? Color = null,
    int? Damage = null);

/// <summary>One line of the log; its parts joined give the matching entry of <see cref="BattlePage.Lines"/>.</summary>
/// <param name="IsRound">A „Runde N“ heading.</param>
/// <remarks>
/// Defaults are left out of the JSON: a big boss fight has hundreds of parts (review feat/fight-log-colours). Only
/// here — site-wide, „Ignore defaults“ would also drop meaningful zeros such as <c>hpLeft: 0</c>.
/// </remarks>
public sealed record LogLine(
    IReadOnlyList<LogPart> Parts,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool IsRound = false);

/// <summary>
/// Text, optionally part of one fighter's label („Pikachu (Ash, HP 24)“): <c>Sides[Side].Fighters[Fighter]</c> of the
/// page.
/// </summary>
/// <param name="IsOwner">The owner within the label („Ash“, „Boss“).</param>
/// <param name="IsSpecies">The species within the label („Pikachu“); false in older files.</param>
/// <param name="IsCritical">„Volltreffer!“; false in older files.</param>
public sealed record LogPart(
    string Text,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Side = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Fighter = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool IsOwner = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool IsSpecies = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool IsCritical = false);

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

/// <summary>
/// <c>statistik.json</c>: rankings beyond the leaderboard (feat/statistics-page). Every list holds at most ten rows
/// with a value above zero; equal values share a rank.
/// </summary>
/// <param name="BossParticipations">Boss fights taken part in.</param>
/// <param name="PvpFights">PvP fights only — boss fights have their own list, NPC fights do not count.</param>
/// <param name="BadLuck">„Unglücksraben“: the longest run of boss fights without a catch.</param>
/// <param name="SingleOwnedCount">All species exactly one trainer owns; <paramref name="SingleOwned"/> lists ten of them.</param>
/// <param name="SingleOwned">Rarest first (legendary, rare, common), then by dex number.</param>
/// <param name="Rivals">Trainer pairs with the most PvP fights against each other.</param>
/// <param name="BossesSeen">Boss species by how often they appeared.</param>
/// <param name="BossesEscaped">Boss species by how often they escaped.</param>
public sealed record Statistics(
    DateTimeOffset UpdatedAt,
    CommunityTotals Community,
    IReadOnlyList<TrainerCount> BossParticipations,
    IReadOnlyList<PvpRecord> PvpFights,
    IReadOnlyList<BadLuckStreak> BadLuck,
    int SingleOwnedCount,
    IReadOnlyList<SingleOwned> SingleOwned,
    IReadOnlyList<Rivalry> Rivals,
    IReadOnlyList<BossTally> BossesSeen,
    IReadOnlyList<BossTally> BossesEscaped);

/// <summary>The head of the statistics page: what the community has together.</summary>
/// <param name="Species">Distinct species owned by anyone.</param>
/// <param name="SpeciesTotal">All species of the game (151).</param>
/// <param name="Catches">Boss catches: survivors of a won boss fight.</param>
public sealed record CommunityTotals(int Species, int SpeciesTotal, int Trainers, int Battles, int Catches);

/// <param name="Color">Twitch chat colour (<c>#RRGGBB</c>) as picked; null if unknown.</param>
public sealed record TrainerRef(string TrainerId, string DisplayName, string? Color = null);

public sealed record TrainerCount(int Rank, TrainerRef Trainer, int Count);

public sealed record PvpRecord(int Rank, TrainerRef Trainer, int Fights, int Wins);

/// <param name="Running">The streak is the trainer's current one: their last boss fight was no catch either.</param>
public sealed record BadLuckStreak(int Rank, TrainerRef Trainer, int Length, bool Running);

public sealed record SingleOwned(SpeciesInfo Species, TrainerRef Owner);

/// <param name="First">The one with more wins; on a tie the Twitch user id that sorts first ordinally („10“ before „9“).</param>
public sealed record Rivalry(int Rank, TrainerRef First, TrainerRef Second, int Fights, int FirstWins, int SecondWins);

/// <param name="Count">Appearances in <see cref="Statistics.BossesSeen"/>, escapes in <see cref="Statistics.BossesEscaped"/>.</param>
public sealed record BossTally(int Rank, SpeciesInfo Species, int Count);

/// <summary>What the local OBS overlay shows: the latest fight and the top of the leaderboard.</summary>
public sealed record OverlayState(BattleSummary? LatestBattle, IReadOnlyList<LeaderboardEntry> Top);
