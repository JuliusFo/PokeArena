using Microsoft.EntityFrameworkCore;
using PokeArena.Contracts;
using PokeArena.Core.Data;

namespace PokeArena.Core.Publishing;

/// <summary>
/// Queues site files for upload (spec §10). Called by game logic inside its own transaction, so a fight and the
/// files it changes are committed together and a network failure loses nothing. In the dry run the rows land in
/// the sandbox database, which the publish worker never reads.
/// </summary>
public static class SiteOutbox
{
    /// <summary>
    /// Files that change with every new or changed trainer — and so with every fight. The statistics show names and
    /// fight counts, so they go along (feat/statistics-page).
    /// </summary>
    public static IEnumerable<string> ForTrainers(IEnumerable<string> trainerIds) =>
        [SitePaths.Leaderboard, SitePaths.Statistics, SitePaths.Meta, .. trainerIds.Select(SitePaths.Trainer)];

    /// <summary>Files that change with a new fight: the fight itself, the index and everyone who took part.</summary>
    public static IEnumerable<string> ForBattle(long battleId, IEnumerable<string> trainerIds) =>
        [SitePaths.Battle(battleId), SitePaths.BattleIndex, .. ForTrainers(trainerIds)];

    /// <summary>Adds the paths or queues them again. Works on the caller's context; the caller saves.</summary>
    public static async Task EnqueueAsync(PokeArenaDb db, IEnumerable<string> paths, DateTimeOffset now, CancellationToken ct)
    {
        var wanted = paths.Distinct().ToList();
        var existing = await db.PublishQueue.Where(i => wanted.Contains(i.Path)).ToDictionaryAsync(i => i.Path, ct);
        foreach (var path in wanted)
        {
            if (existing.TryGetValue(path, out var item))
            {
                item.Revision++;
                item.QueuedAt = now;
                item.NextAttemptAt = now;
                item.Attempts = 0;
                item.LastError = null;
            }
            else
            {
                db.PublishQueue.Add(new PublishQueueItem { Path = path, QueuedAt = now, NextAttemptAt = now });
            }
        }
    }

    /// <summary>
    /// „Alles neu veröffentlichen“: every file the database can produce (spec §9), plus <paramref name="inBucket"/> —
    /// files found in the bucket. Those without a source in the database (after restoring an older backup) are
    /// deleted by the worker (review 1.0.3, A6).
    /// </summary>
    public static async Task EnqueueEverythingAsync(PokeArenaDb db, IEnumerable<string> inBucket, DateTimeOffset now, CancellationToken ct)
    {
        var trainers = await db.Trainers.Select(t => t.TwitchUserId).ToListAsync(ct);
        var battles = await db.Battles.Select(b => b.Id).ToListAsync(ct);
        var images = await db.ProfileImages.Select(i => new { i.TrainerId, i.ContentType }).ToListAsync(ct);
        await EnqueueAsync(
            db,
            [
                SitePaths.BattleIndex,
                .. ForTrainers(trainers),
                .. battles.Select(SitePaths.Battle),
                .. images.Select(i => SitePaths.ProfileImage(i.TrainerId, i.ContentType)),
                .. inBucket,
            ],
            now,
            ct);
    }
}
