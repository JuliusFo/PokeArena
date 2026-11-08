using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PokeArena.Contracts;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;

namespace PokeArena.Core.Publishing;

/// <summary>A rendered site file and how long browsers and the CDN may keep it.</summary>
public sealed record SiteFile(byte[] Content, string CacheControl);

/// <summary>
/// Renders the website's JSON files from the database (spec §9). Every file is derived: rendering the same path
/// again gives the current state, so the outbox only needs paths and "republish everything" is just a full queue.
/// </summary>
public sealed class SiteGenerator(SpeciesCatalog species, NpcCatalog npcs)
{
    private readonly BattleTexts _texts = new(species, npcs);

    /// <summary>Entries of <c>kaempfe.json</c>.</summary>
    public const int BattleIndexSize = 50;

    /// <summary>Fights listed on a trainer page.</summary>
    public const int RecentBattlesPerTrainer = 10;

    /// <summary>Leaderboard rows the OBS overlay shows.</summary>
    public const int OverlayTopSize = 5;

    /// <summary>
    /// Fights change only when a trainer in them is deleted (spec §11); a day keeps the cache useful and lets the
    /// deleted name drop out of Cloudflare and browsers without a purge (WP-08 decision).
    /// </summary>
    public const string BattleCache = "public, max-age=86400";

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
                ? new SiteFile(JsonSerializer.SerializeToUtf8Bytes(battle, SiteJson.Default.BattlePage), BattleCache)
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
        var texts = _texts.Describe(battle, log);
        var sides = log.Fighters
            .GroupBy(f => f.Side)
            .OrderBy(g => g.Key)
            .Select(g => new BattleSide(
                g.Key == 0 ? texts.TrainerSide : texts.OpponentSide,
                g.Key == 0 ? texts.TrainerSideWon : !texts.TrainerSideWon,
                [.. g.Select(f => new BattleFighter(Info(f.SpeciesId), f.Owner, f.TrainerId, f.MaxHp))]))
            .ToList();
        return new BattlePage(battle.Id, battle.Kind, battle.FoughtAt, battle.Seed, texts.Headline, texts.Result, sides, log.ToLines(species));
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
            var texts = _texts.Describe(b, BattleLog.FromJson(b.LogJson));
            return new BattleSummary(b.Id, b.Kind, b.FoughtAt, texts.Headline, texts.Result);
        })];
    }

    private SpeciesInfo Info(int speciesId)
    {
        var s = species.Get(speciesId);
        return new SpeciesInfo(s.Id, s.Name, s.Types);
    }

    private static SiteFile Mutable(byte[] content) => new(content, MutableCache);
}
