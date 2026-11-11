using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PokeArena.Contracts;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;

namespace PokeArena.Core.Publishing;

/// <summary>A rendered site file, how long browsers and the CDN may keep it, and its media type.</summary>
public sealed record SiteFile(byte[] Content, string CacheControl, string ContentType = SiteFile.Json)
{
    public const string Json = "application/json; charset=utf-8";
}

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

    /// <summary>Rows per list of <c>statistik.json</c>.</summary>
    public const int StatisticsSize = 10;

    /// <summary>Leaderboard rows the OBS overlay shows.</summary>
    public const int OverlayTopSize = 5;

    /// <summary>
    /// Fights change only when a trainer in them is deleted (spec §11); a day keeps the cache useful and lets the
    /// deleted name drop out of Cloudflare and browsers without a purge (WP-08 decision).
    /// </summary>
    public const string BattleCache = "public, max-age=86400";

    /// <summary>Below the site's 15 s poll interval (spec §9, §13), so nobody sees a stale leaderboard for long.</summary>
    public const string MutableCache = "public, max-age=10";

    /// <summary>
    /// Profile pictures keep their path when they change, and the data host refuses query strings, so there is no
    /// cache busting: like fights, a new or deleted picture is gone from Cloudflare and browsers after a day
    /// (feat/trainer-profile-image).
    /// </summary>
    public const string ImageCache = BattleCache;

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
            case SitePaths.Statistics:
                return Mutable(JsonSerializer.SerializeToUtf8Bytes(await StatisticsAsync(db, now, ct), SiteJson.Default.Statistics));
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

        if (SitePaths.ProfileImageOf(path) is ({ } imageTrainerId, { } contentType))
        {
            // A path in the other format (the trainer uploaded a picture of a new kind) has no source any more.
            var image = await db.ProfileImages.AsNoTracking()
                .Where(i => i.TrainerId == imageTrainerId && i.ContentType == contentType)
                .Select(i => i.Content)
                .FirstOrDefaultAsync(ct);
            return image is null ? null : new SiteFile(image, ImageCache, contentType);
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
            .Select(t => new
            {
                t.TwitchUserId,
                t.DisplayName,
                t.ChatColor,
                ImageType = db.ProfileImages.Where(i => i.TrainerId == t.TwitchUserId).Select(i => i.ContentType).FirstOrDefault(),
                Specimens = t.Specimens.Select(s => new { s.SpeciesId, s.AcquiredAt }).ToList(),
            })
            .ToListAsync(ct);
        var ranked = trainers
            .Select(t => new
            {
                t.TwitchUserId,
                t.DisplayName,
                t.ChatColor,
                t.ImageType,
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
            .Select((t, i) => new LeaderboardEntry(
                i + 1,
                t.TwitchUserId,
                t.DisplayName,
                t.Distinct,
                t.Total,
                t.ChatColor,
                ImagePath(t.TwitchUserId, t.ImageType)))
            .ToList();
        return new Leaderboard(now, ranked.Count, ranked);
    }

    /// <summary>
    /// The statistics page (feat/statistics-page): only participant rows, battle headers and owners — no fight log is
    /// read. Deleted trainers drop out by themselves: their participant rows and specimens are gone.
    /// </summary>
    public async Task<Statistics> StatisticsAsync(PokeArenaDb db, DateTimeOffset now, CancellationToken ct)
    {
        var trainers = await db.Trainers.AsNoTracking()
            .Select(t => new TrainerRef(t.TwitchUserId, t.DisplayName, t.ChatColor))
            .ToDictionaryAsync(t => t.TrainerId, ct);
        var owners = await db.Specimens.AsNoTracking()
            .Select(s => new { s.SpeciesId, s.TrainerId })
            .Distinct()
            .ToListAsync(ct);
        var battles = await db.Battles.AsNoTracking()
            .Select(b => new { b.Id, b.Kind, b.FoughtAt, b.BossSpeciesId, b.Outcome })
            .ToDictionaryAsync(b => b.Id, ct);
        var rows = await db.BattleParticipants.AsNoTracking()
            .Select(p => new { p.BattleId, p.TrainerId, p.Won })
            .ToListAsync(ct);
        var participants = rows
            .Where(p => trainers.ContainsKey(p.TrainerId))
            .Select(p => (Battle: battles[p.BattleId], Trainer: trainers[p.TrainerId], p.Won))
            .ToList();
        var boss = participants.Where(p => p.Battle.Kind == BattleKind.Boss).ToList();
        var pvp = participants.Where(p => p.Battle.Kind == BattleKind.Pvp).ToList();

        var bossParticipations = Ranked(
            boss.GroupBy(p => p.Trainer).Select(g => (Trainer: g.Key, Count: g.Count())),
            x => x.Count, x => x.Trainer.DisplayName, x => x.Trainer.TrainerId,
            (rank, x) => new TrainerCount(rank, x.Trainer, x.Count));

        var pvpFights = Ranked(
            pvp.GroupBy(p => p.Trainer).Select(g => (Trainer: g.Key, Fights: g.Count(), Wins: g.Count(p => p.Won))),
            x => x.Fights, x => x.Trainer.DisplayName, x => x.Trainer.TrainerId,
            (rank, x) => new PvpRecord(rank, x.Trainer, x.Fights, x.Wins));

        var badLuck = Ranked(
            boss.GroupBy(p => p.Trainer).Select(g =>
            {
                int longest = 0, current = 0;
                foreach (var p in g.OrderBy(p => p.Battle.FoughtAt).ThenBy(p => p.Battle.Id))
                {
                    current = p.Won ? 0 : current + 1;
                    longest = Math.Max(longest, current);
                }

                return (Trainer: g.Key, Length: longest, Running: longest > 0 && current == longest);
            }),
            x => x.Length, x => x.Trainer.DisplayName, x => x.Trainer.TrainerId,
            (rank, x) => new BadLuckStreak(rank, x.Trainer, x.Length, x.Running));

        var single = owners
            .GroupBy(o => o.SpeciesId)
            .Where(g => g.Count() == 1 && trainers.ContainsKey(g.First().TrainerId))
            .Select(g => (Species: species.Get(g.Key), Owner: trainers[g.First().TrainerId]))
            .OrderByDescending(x => x.Species.Rarity)
            .ThenBy(x => x.Species.Id)
            .ToList();

        var rivals = Ranked(
            pvp.GroupBy(p => p.Battle.Id)
                .Where(g => g.Count() == 2)
                .Select(g => g.OrderBy(p => p.Trainer.TrainerId, StringComparer.Ordinal).ToList())
                .GroupBy(pair => (A: pair[0].Trainer, B: pair[1].Trainer))
                .Select(g =>
                {
                    var (a, b) = g.Key;
                    var winsA = g.Count(pair => pair[0].Won);
                    var winsB = g.Count(pair => pair[1].Won);
                    return winsB > winsA
                        ? (First: b, Second: a, Fights: g.Count(), FirstWins: winsB, SecondWins: winsA)
                        : (First: a, Second: b, Fights: g.Count(), FirstWins: winsA, SecondWins: winsB);
                }),
            x => x.Fights, x => x.First.DisplayName, x => x.First.TrainerId + "/" + x.Second.TrainerId,
            (rank, x) => new Rivalry(rank, x.First, x.Second, x.Fights, x.FirstWins, x.SecondWins));

        var bossFights = battles.Values.Where(b => b.Kind == BattleKind.Boss && b.BossSpeciesId is not null).ToList();
        IReadOnlyList<BossTally> Bosses(Func<BattleOutcome, bool> counts) => Ranked(
            bossFights.Where(b => counts(b.Outcome)).GroupBy(b => b.BossSpeciesId!.Value).Select(g => (Species: g.Key, Count: g.Count())),
            x => x.Count, x => species.Get(x.Species).Name, x => x.Species.ToString("D3", CultureInfo.InvariantCulture),
            (rank, x) => new BossTally(rank, Info(x.Species), x.Count));

        return new Statistics(
            now,
            new CommunityTotals(
                owners.Select(o => o.SpeciesId).Distinct().Count(),
                species.All.Count,
                trainers.Count,
                battles.Count,
                boss.Count(p => p.Won)),
            bossParticipations,
            pvpFights,
            badLuck,
            single.Count,
            [.. single.Take(StatisticsSize).Select(x => new SingleOwned(Info(x.Species.Id), x.Owner))],
            rivals,
            Bosses(_ => true),
            Bosses(o => o == BattleOutcome.BossEscaped));
    }

    /// <summary>
    /// The top <see cref="StatisticsSize"/> of <paramref name="items"/> with a value above zero, highest first; equal
    /// values share a rank (1, 1, 3) and are ordered by name, then by <paramref name="key"/> so the order is stable.
    /// </summary>
    private static List<TResult> Ranked<T, TResult>(
        IEnumerable<T> items, Func<T, int> value, Func<T, string> name, Func<T, string> key, Func<int, T, TResult> make)
    {
        var all = items.Where(x => value(x) > 0).ToList();
        var values = all.Select(value).ToList();
        return [.. all
            .OrderByDescending(value)
            .ThenBy(name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(key, StringComparer.Ordinal)
            .Take(StatisticsSize)
            .Select(x => make(1 + values.Count(v => v > value(x)), x))];
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
            .Select(t => new
            {
                t.DisplayName,
                t.RegisteredAt,
                t.Tokens,
                t.ChatColor,
                ImageType = db.ProfileImages.Where(i => i.TrainerId == t.TwitchUserId).Select(i => i.ContentType).FirstOrDefault(),
                Specimens = t.Specimens.Select(s => new { s.SpeciesId, s.IsStarter }).ToList(),
            })
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
            recent,
            ImagePath(trainerId, trainer.ImageType),
            trainer.ChatColor);
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
        var trainerIds = log.Fighters.Select(f => f.TrainerId).OfType<string>().Distinct().ToList();
        var colors = await db.Trainers.AsNoTracking()
            .Where(t => trainerIds.Contains(t.TwitchUserId) && t.ChatColor != null)
            .ToDictionaryAsync(t => t.TwitchUserId, t => t.ChatColor!, ct);
        var groups = log.Fighters
            .Select((f, i) => (Fighter: f, Index: i))
            .GroupBy(x => x.Fighter.Side)
            .OrderBy(g => g.Key)
            .Select(g => g.ToList())
            .ToList();
        var damage = log.DamageDealt();
        var sides = groups
            .Select(g => new BattleSide(
                g[0].Fighter.Side == 0 ? texts.TrainerSide : texts.OpponentSide,
                g[0].Fighter.Side == 0 ? texts.TrainerSideWon : !texts.TrainerSideWon,
                [.. g.Select(x => new BattleFighter(
                    Info(x.Fighter.SpeciesId), x.Fighter.Owner, x.Fighter.TrainerId, x.Fighter.MaxHp, log.HpLeft(x.Index), log.WasUsed(x.Index),
                    x.Fighter.TrainerId is { } id ? colors.GetValueOrDefault(id) : null, damage[x.Index]))]))
            .ToList();

        // Log index → position on the page (side, fighter within the side).
        var position = groups
            .SelectMany((g, side) => g.Select((x, fighter) => (x.Index, Side: side, Fighter: fighter)))
            .ToDictionary(x => x.Index, x => (x.Side, x.Fighter));
        var text = log.ToText(species);
        var parts = text
            .Select(line => new LogLine(
                [.. line.Spans.Select(s => s.Fighter is { } index
                    ? new LogPart(s.Text, position[index].Side, position[index].Fighter, s.IsOwner, s.IsSpecies)
                    : new LogPart(s.Text, IsCritical: s.IsCritical))],
                line.IsRound))
            .ToList();
        return new BattlePage(
            battle.Id, battle.Kind, battle.FoughtAt, battle.Seed, texts.Headline, texts.Result, sides, [.. text.Select(l => l.Text)], parts);
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

    /// <summary>
    /// The site path of a stored picture, or null without one. A format the site does not know (only a hand-edited
    /// database could hold one) counts as no picture: it must not break the leaderboard for everyone (review
    /// feat/leaderboard-profile-colours).
    /// </summary>
    private static string? ImagePath(string trainerId, string? contentType) =>
        contentType is SitePaths.Png or SitePaths.Jpeg ? SitePaths.ProfileImage(trainerId, contentType) : null;

    private SpeciesInfo Info(int speciesId)
    {
        var s = species.Get(speciesId);
        return new SpeciesInfo(s.Id, s.Name, s.Types);
    }

    private static SiteFile Mutable(byte[] content) => new(content, MutableCache);
}
