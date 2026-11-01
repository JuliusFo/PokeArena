using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

    private PublishState _state = new(false, 0, null, null);

    public event Action? Changed;

    public PublishState State => Volatile.Read(ref _state);

    /// <summary>„Alles neu veröffentlichen“: queues every file of the live database (spec §9).</summary>
    public async Task RepublishEverythingAsync(CancellationToken ct)
    {
        await using var db = database.OpenLive();
        await SiteOutbox.EnqueueEverythingAsync(db, time.GetUtcNow(), ct);
        await db.SaveChangesAsync(ct);
        log.LogInformation("Queued every site file for upload");
    }

    /// <summary>One round: uploads the files that are due. Public for tests; the service calls it in a loop.</summary>
    public async Task PublishDueAsync(CancellationToken ct)
    {
        var publisher = publishers.Create();
        var now = time.GetUtcNow();
        await using var db = database.OpenLive();
        var due = publisher is null
            ? []
            : await db.PublishQueue.AsNoTracking()
                .Where(i => i.NextAttemptAt <= now)
                .OrderBy(i => i.NextAttemptAt)
                .ThenBy(i => i.QueuedAt)
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
