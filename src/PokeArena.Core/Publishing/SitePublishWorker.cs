using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PokeArena.Contracts;
using PokeArena.Core.Data;

namespace PokeArena.Core.Publishing;

/// <summary>What the desktop app shows about the website upload.</summary>
/// <param name="IsSetUp">Bucket configured and R2 token stored.</param>
/// <param name="Pending">Files waiting for upload.</param>
/// <param name="LastError">Plain German reason of the last failure; null once an upload succeeds again.</param>
public sealed record PublishState(bool IsSetUp, int Pending, string? LastError, DateTimeOffset? LastUploadAt);

/// <summary>
/// Drains the website outbox of the live database (spec §10): renders each queued file from the current data and
/// uploads it, or deletes it when its source is gone. Failures back off per file up to <see cref="MaxBackoff"/> and
/// never give up — a long outage only delays the site. The dry run's sandbox is never published.
/// </summary>
public sealed class SitePublishWorker(
    GameDatabase database,
    SiteGenerator generator,
    ISitePublisherFactory publishers,
    TimeProvider time,
    ILogger<SitePublishWorker> log) : BackgroundService
{
    /// <summary>How often the outbox is checked; fights are on the site a few seconds after the chat message.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    public static readonly TimeSpan FirstBackoff = TimeSpan.FromSeconds(5);

    /// <summary>Also caps the cost of a bug that keeps failing: at most a few requests per file and hour.</summary>
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(15);

    /// <summary>Files per round, so a full republish does not block the status for minutes.</summary>
    private const int BatchSize = 25;

    /// <summary>
    /// How far fight ids in the bucket may run ahead of the database after restoring a backup. A file further ahead
    /// was not written by this bot (review 1.0.3, second pass) and must not raise the id counter.
    /// </summary>
    internal const long MaxBattleIdGap = 100_000;

    private PublishState _state = new(false, 0, null, null);
    private int _retryAll;
    private int _subscribed;

    public event Action? Changed;

    public PublishState State => Volatile.Read(ref _state);

    /// <summary>
    /// „Alles neu veröffentlichen“: queues every file of the live database (spec §9) and every trainer or fight file
    /// in the bucket, so files the database no longer knows (an older backup was restored) get deleted. Fight ids
    /// then continue above the highest one in the bucket: a reused id would show the old, cached fight for a day.
    /// </summary>
    public async Task RepublishEverythingAsync(CancellationToken ct)
    {
        var inBucket = await ListBucketAsync(ct);
        await using var db = database.OpenLive();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await SiteOutbox.EnqueueEverythingAsync(db, inBucket, time.GetUtcNow(), ct);
        var highestInDb = await db.Battles.MaxAsync(b => (long?)b.Id, ct) ?? 0;
        var highestInBucket = inBucket.Select(SitePaths.BattleIdOf).Max() ?? 0;
        if (highestInBucket > highestInDb + MaxBattleIdGap)
        {
            // Not from an older backup of this database: never let such a file push the counter towards its end.
            log.LogWarning("Fight {Id} in the bucket is far above the database ({Highest}); ids are not raised", highestInBucket, highestInDb);
        }
        else if (highestInBucket > highestInDb)
        {
            await KeepBattleIdsAboveAsync(db, highestInBucket, ct);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        log.LogInformation("Queued every site file for upload ({InBucket} found in the bucket)", inBucket.Count);
    }

    /// <summary>One round: uploads the files that are due. Public for tests; the service calls it in a loop.</summary>
    public async Task PublishDueAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _subscribed, 1) == 0)
        {
            publishers.Changed += () => Interlocked.Exchange(ref _retryAll, 1);
        }

        var publisher = publishers.Create();
        var now = time.GetUtcNow();
        await using var db = database.OpenLive();
        if (Interlocked.Exchange(ref _retryAll, 0) == 1)
        {
            // New access: the old failures say nothing about it.
            await db.PublishQueue.ExecuteUpdateAsync(
                s => s.SetProperty(i => i.NextAttemptAt, now).SetProperty(i => i.Attempts, 0).SetProperty(i => i.LastError, (string?)null),
                ct);
            Publish(State with { LastError = null });
        }

        // Fresh changes first, newest first: a fight posted in chat must not wait behind a full republish
        // (review 1.0.3, A7). Files that failed before come after them.
        var due = publisher is null
            ? []
            : await db.PublishQueue.AsNoTracking()
                .Where(i => i.NextAttemptAt <= now)
                .OrderBy(i => i.Attempts == 0 ? 0 : 1)
                .ThenByDescending(i => i.QueuedAt)
                .Take(BatchSize)
                .ToListAsync(ct);

        var state = State with { IsSetUp = publisher is not null };
        foreach (var item in due)
        {
            try
            {
                if (await generator.RenderAsync(db, item.Path, now, ct) is { } file)
                {
                    await publisher!.PutAsync(item.Path, file, ct);
                }
                else
                {
                    await publisher!.DeleteAsync(item.Path, ct);
                }

                // Queued again while uploading: keep the row, the next round uploads the newer state.
                await db.PublishQueue.Where(i => i.Path == item.Path && i.Revision == item.Revision).ExecuteDeleteAsync(ct);
                state = state with { LastError = null, LastUploadAt = now };
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                var reason = ex is SitePublishException ? ex.Message : "Unerwarteter Fehler beim Hochladen (Details im Log).";
                log.LogWarning(ex, "Upload of {Path} failed (attempt {Attempt})", item.Path, item.Attempts + 1);
                var retryAt = now + Backoff(item.Attempts + 1);
                await db.PublishQueue
                    .Where(i => i.Path == item.Path && i.Revision == item.Revision)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(i => i.Attempts, i => i.Attempts + 1)
                            .SetProperty(i => i.NextAttemptAt, retryAt)
                            .SetProperty(i => i.LastError, reason),
                        ct);
                state = state with { LastError = reason };

                // The next file would most likely fail the same way (offline, wrong key).
                break;
            }
        }

        Publish(state with { Pending = await db.PublishQueue.CountAsync(ct) });
    }

    private async Task<IReadOnlyList<string>> ListBucketAsync(CancellationToken ct)
    {
        if (publishers.Create() is not { } publisher)
        {
            return [];
        }

        try
        {
            var keys = (await publisher.ListAsync(SitePaths.TrainerPrefix, ct)).Concat(await publisher.ListAsync(SitePaths.BattlePrefix, ct)).ToList();
            var paths = keys.Where(IsOwnPath).ToList();
            if (paths.Count < keys.Count)
            {
                // Anyone with the R2 key can put files there; only what the bot itself would write is queued.
                log.LogWarning("Ignored {Count} files in the bucket that the bot does not write", keys.Count - paths.Count);
            }

            return paths;
        }
        catch (SitePublishException ex)
        {
            // The database's own files still go out; only leftovers stay until the next republish.
            log.LogWarning(ex, "Listing the bucket failed, republishing without it");
            return [];
        }
    }

    /// <summary>A trainer page of a numeric Twitch id or a fight page, exactly as <see cref="SitePaths"/> writes it.</summary>
    private static bool IsOwnPath(string key) =>
        SitePaths.BattleIdOf(key) is { } battleId
            ? key == SitePaths.Battle(battleId)
            : SitePaths.TrainerIdOf(key) is { Length: > 0 } trainerId && trainerId.All(char.IsAsciiDigit) && key == SitePaths.Trainer(trainerId);

    /// <summary>Raises the AUTOINCREMENT counter of the fights so the next id is above <paramref name="highest"/>.</summary>
    private static async Task KeepBattleIdsAboveAsync(PokeArenaDb db, long highest, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO sqlite_sequence (name, seq) SELECT 'Battles', 0 WHERE NOT EXISTS (SELECT 1 FROM sqlite_sequence WHERE name = 'Battles')",
            ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE sqlite_sequence SET seq = MAX(seq, {highest}) WHERE name = 'Battles'",
            ct);
    }

    internal static TimeSpan Backoff(int attempts)
    {
        var seconds = FirstBackoff.TotalSeconds * Math.Pow(2, Math.Min(attempts - 1, 20));
        return seconds >= MaxBackoff.TotalSeconds ? MaxBackoff : TimeSpan.FromSeconds(seconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Website publishing round failed");
            }

            try
            {
                await Task.Delay(PollInterval, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void Publish(PublishState state)
    {
        if (Interlocked.Exchange(ref _state, state) != state)
        {
            Changed?.Invoke();
        }
    }
}
