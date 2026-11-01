using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PokeArena.Contracts;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;
using DataBattleKind = PokeArena.Core.Data.BattleKind;
using SiteBattleKind = PokeArena.Contracts.BattleKind;

namespace PokeArena.Core.Publishing;

/// <summary>A rendered site file and how long browsers and the CDN may keep it.</summary>
public sealed record SiteFile(byte[] Content, string CacheControl);

/// <summary>
/// Renders the website's JSON files from the database (spec §9). Every file is derived: rendering the same path
/// again gives the current state, so the outbox only needs paths and "republish everything" is just a full queue.
/// </summary>
public sealed class SiteGenerator(SpeciesCatalog species, NpcCatalog npcs)
{
    /// <summary>Entries of <c>kaempfe.json</c>.</summary>
    public const int BattleIndexSize = 50;

    /// <summary>Fights listed on a trainer page.</summary>
    public const int RecentBattlesPerTrainer = 10;

    /// <summary>Leaderboard rows the OBS overlay shows.</summary>
    public const int OverlayTopSize = 5;

    /// <summary>Fights never change once stored (ADR-0004).</summary>
    public const string ImmutableCache = "public, max-age=31536000, immutable";

    /// <summary>Below the site's 15 s poll interval (spec §9, §13), so nobody sees a stale leaderboard for long.</summary>
    public const string MutableCache = "public, max-age=10";

    /// <summary>The file at <paramref name="path"/>, or null if it no longer exists (deleted trainer) and must be removed.</summary>
    /// <exception cref="ArgumentException">Not a site path.</exception>
    public async Task<SiteFile?> RenderAsync(PokeArenaDb db, string path, DateTimeOffset now, CancellationToken ct)
    {
        switch (path)
        {
            case SitePaths.Meta:
                return Mutable(JsonSerializer.SerializeToUtf8Bytes(new SiteMeta(SiteMeta.CurrentFormatVersion, now), SiteJson.Default.SiteMeta));
            case SitePaths.Leaderboard:
                return Mutable(JsonSerializer.SerializeToUtf8Bytes(await LeaderboardAsync(db, now, ct), SiteJson.Default.Leaderboard));
            case SitePaths.BattleIndex:
                var index = new BattleIndex(now, await LatestBattlesAsync(db, b => true, BattleIndexSize, ct));
                return Mutable(JsonSerializer.SerializeToUtf8Bytes(index, SiteJson.Default.BattleIndex));
        }

        if (SitePaths.TrainerIdOf(path) is { } trainerId)
        {
            return await TrainerAsync(db, trainerId, now, ct) is { } trainer
                ? Mutable(JsonSerializer.SerializeToUtf8Bytes(trainer, SiteJson.Default.TrainerPage))
                : null;
        }

        if (SitePaths.BattleIdOf(path) is { } battleId)
        {
            return await BattleAsync(db, battleId, ct) is { } battle
                ? new SiteFile(JsonSerializer.SerializeToUtf8Bytes(battle, SiteJson.Default.BattlePage), ImmutableCache)
                : null;
        }

        throw new ArgumentException($"Not a site path: {path}", nameof(path));
    }

    /// <summary>
    /// Every trainer, ranked by distinct species, then all specimens, then earliest attainment: the moment the
    /// trainer last completed the set of species they own now (spec §9).
    /// </summary>
    public async Task<Leaderboard> LeaderboardAsync(PokeArenaDb db, DateTimeOffset now, CancellationToken ct)
    {
        var trainers = await db.Trainers.AsNoTracking()
            .Select(t => new { t.TwitchUserId, t.DisplayName, Specimens = t.Specimens.Select(s => new { s.SpeciesId, s.AcquiredAt }).ToList() })
            .ToListAsync(ct);
        var ranked = trainers
            .Select(t => new
            {
                t.TwitchUserId,
                t.DisplayName,
                Distinct = t.Specimens.Select(s => s.SpeciesId).Distinct().Count(),
                Total = t.Specimens.Count,
                Attained = t.Specimens.Count == 0
                    ? DateTimeOffset.MaxValue
                    : t.Specimens.GroupBy(s => s.SpeciesId).Max(g => g.Min(s => s.AcquiredAt)),
            })
            .OrderByDescending(t => t.Distinct)
            .ThenByDescending(t => t.Total)
            .ThenBy(t => t.Attained)
            .ThenBy(t => t.TwitchUserId, StringComparer.Ordinal)
            .Select((t, i) => new LeaderboardEntry(i + 1, t.TwitchUserId, t.DisplayName, t.Distinct, t.Total))
            .ToList();
        return new Leaderboard(now, ranked.Count, ranked);
    }

    /// <summary>The local OBS overlay: latest fight and the top of the leaderboard.</summary>
    public async Task<OverlayState> OverlayAsync(PokeArenaDb db, DateTimeOffset now, CancellationToken ct)
    {
        var latest = await LatestBattlesAsync(db, b => true, 1, ct);
        var leaderboard = await LeaderboardAsync(db, now, ct);
        return new OverlayState(latest.FirstOrDefault(), [.. leaderboard.Entries.Take(OverlayTopSize)]);
    }

    private async Task<TrainerPage?> TrainerAsync(PokeArenaDb db, string trainerId, DateTimeOffset now, CancellationToken ct)
    {
        var trainer = await db.Trainers.AsNoTracking()
            .Where(t => t.TwitchUserId == trainerId)
            .Select(t => new { t.DisplayName, t.RegisteredAt, t.Tokens, Specimens = t.Specimens.Select(s => new { s.SpeciesId, s.IsStarter }).ToList() })
            .FirstOrDefaultAsync(ct);
        if (trainer is null)
        {
            return null;
        }

        var achievements = await db.AchievementUnlocks.AsNoTracking()
            .Where(u => u.TrainerId == trainerId)
            .OrderBy(u => u.Threshold)
            .Select(u => new { u.Threshold, u.UnlockedAt })
            .ToListAsync(ct);
        var badges = await db.NpcBadges.AsNoTracking()
            .Where(b => b.TrainerId == trainerId)
            .OrderBy(b => b.WonAt)
            .Select(b => new { b.NpcId, b.WonAt, b.BattleId })
            .ToListAsync(ct);
        var fights = await db.BattleParticipants.AsNoTracking()
            .Where(p => p.TrainerId == trainerId)
            .GroupBy(p => 1)
            .Select(g => new { Battles = g.Count(), Wins = g.Count(p => p.Won) })
            .FirstOrDefaultAsync(ct);
        var recent = await LatestBattlesAsync(db, b => b.Participants.Any(p => p.TrainerId == trainerId), RecentBattlesPerTrainer, ct);

        return new TrainerPage(
            trainerId,
            trainer.DisplayName,
            trainer.RegisteredAt,
            now,
            trainer.Specimens.Select(s => s.SpeciesId).Distinct().Count(),
            trainer.Specimens.Count,
            trainer.Tokens,
            fights?.Battles ?? 0,
            fights?.Wins ?? 0,
            [.. trainer.Specimens
                .GroupBy(s => s.SpeciesId)
                .OrderBy(g => g.Key)
                .Select(g => new PokedexEntry(Info(g.Key), g.Count(), g.Any(s => s.IsStarter)))],
            [.. achievements.Select(a => new AchievementInfo(a.Threshold, npcs.ForThreshold(a.Threshold).Name, a.UnlockedAt))],
            [.. badges.Select(b => new BadgeInfo(npcs.Find(b.NpcId)?.Name ?? b.NpcId, b.WonAt, b.BattleId))],
            recent);
    }

    private async Task<BattlePage?> BattleAsync(PokeArenaDb db, long battleId, CancellationToken ct)
    {
        var battle = await db.Battles.AsNoTracking().Include(b => b.Participants).FirstOrDefaultAsync(b => b.Id == battleId, ct);
        if (battle is null)
        {
            return null;
        }

        var log = BattleLog.FromJson(battle.LogJson);
        var texts = Describe(battle, log);
        var sides = log.Fighters
            .GroupBy(f => f.Side)
            .OrderBy(g => g.Key)
            .Select(g => new BattleSide(
                g.Key == 0 ? texts.TrainerSide : texts.OpponentSide,
                g.Key == 0 ? texts.TrainerSideWon : !texts.TrainerSideWon,
                [.. g.Select(f => new BattleFighter(Info(f.SpeciesId), f.Owner, f.TrainerId, f.MaxHp))]))
            .ToList();
        return new BattlePage(battle.Id, Kind(battle.Kind), battle.FoughtAt, battle.Seed, texts.Headline, texts.Result, sides, log.ToLines(species));
    }

    private async Task<IReadOnlyList<BattleSummary>> LatestBattlesAsync(
        PokeArenaDb db, System.Linq.Expressions.Expression<Func<Battle, bool>> filter, int limit, CancellationToken ct)
    {
        var battles = await db.Battles.AsNoTracking()
            .Include(b => b.Participants)
            .Where(filter)
            .OrderByDescending(b => b.FoughtAt)
            .ThenByDescending(b => b.Id)
            .Take(limit)
            .ToListAsync(ct);
        return [.. battles.Select(b =>
        {
            var texts = Describe(b, BattleLog.FromJson(b.LogJson));
            return new BattleSummary(b.Id, Kind(b.Kind), b.FoughtAt, texts.Headline, texts.Result);
        })];
    }

    /// <summary>German headline and result of a fight. Names come from the log: as they were at the fight.</summary>
    private BattleTexts Describe(Battle battle, BattleLog log)
    {
        var trainerSide = log.Fighters.Where(f => f.Side == 0).ToList();
        var opponentSide = log.Fighters.Where(f => f.Side == 1).ToList();
        var loot = battle.LootSpeciesId is { } lootId ? species.Get(lootId).Name : null;
        switch (battle.Kind)
        {
            case DataBattleKind.Boss:
            {
                var boss = species.Get(battle.BossSpeciesId ?? opponentSide[0].SpeciesId);
                var defeated = battle.Outcome == BattleOutcome.BossDefeated;
                var catchers = battle.Participants
                    .Where(p => p.Won)
                    .Select(p => trainerSide.FirstOrDefault(f => f.TrainerId == p.TrainerId)?.Owner)
                    .OfType<string>()
                    .ToList();
                var result = defeated
                    ? $"{boss.Name} wurde besiegt! Gefangen von: {string.Join(", ", catchers)}."
                    : $"{boss.Name} ist entkommen.";
                return new BattleTexts($"Boss-Kampf gegen {boss.Name}{Game.BossEncounter.TierSuffix(boss)}", result, "Trainer", boss.Name, defeated);
            }

            case DataBattleKind.Pvp:
            {
                var challenger = trainerSide[0].Owner;
                var opponent = opponentSide[0].Owner;
                var challengerWon = battle.Outcome == BattleOutcome.ChallengerWon;
                var (winner, loser) = challengerWon ? (challenger, opponent) : (opponent, challenger);
                var result = loot is null ? $"{winner} besiegt {loser}." : $"{winner} besiegt {loser} und bekommt {loot} von {loser}!";
                return new BattleTexts($"{challenger} gegen {opponent}", result, challenger, opponent, challengerWon);
            }

            case DataBattleKind.Npc:
            {
                var trainer = trainerSide[0].Owner;
                var npc = battle.NpcId is { } id ? npcs.Find(id)?.Name ?? id : opponentSide[0].Owner;
                var won = battle.Outcome == BattleOutcome.ChallengerWon;
                var result = !won
                    ? $"{npc} besiegt {trainer}."
                    : battle.Tokens is { } tokens
                        ? $"{trainer} besiegt {npc} zum ersten Mal und bekommt den Orden, {Tokens(tokens)}{(loot is null ? "" : $" und {loot}")}!"
                        : $"{trainer} besiegt {npc}.";
                return new BattleTexts($"{trainer} gegen {npc}", result, trainer, npc, won);
            }

            default:
                throw new InvalidOperationException($"Unknown battle kind {battle.Kind}");
        }
    }

    private SpeciesInfo Info(int speciesId)
    {
        var s = species.Get(speciesId);
        return new SpeciesInfo(s.Id, s.Name, s.Types);
    }

    private static SiteFile Mutable(byte[] content) => new(content, MutableCache);

    private static SiteBattleKind Kind(DataBattleKind kind) => kind switch
    {
        DataBattleKind.Boss => SiteBattleKind.Boss,
        DataBattleKind.Pvp => SiteBattleKind.Pvp,
        DataBattleKind.Npc => SiteBattleKind.Npc,
        _ => throw new InvalidOperationException($"Unknown battle kind {kind}"),
    };

    private static string Tokens(int count) => count == 1 ? "1 Token" : $"{count} Tokens";

    /// <param name="TrainerSideWon">Side 0 of the log won: the boss was defeated, the challenger or the trainer won.</param>
    private sealed record BattleTexts(string Headline, string Result, string TrainerSide, string OpponentSide, bool TrainerSideWon);
}
