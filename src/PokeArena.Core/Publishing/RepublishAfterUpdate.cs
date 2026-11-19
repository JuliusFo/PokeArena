using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PokeArena.Core.Publishing;

/// <summary>
/// Re-renders every site file once on the first start of a new version (review 1.8.1, A7): most releases change how
/// older fights or trainer pages look, and the update installs unattended after the stream, so nobody would click
/// „Alles neu veröffentlichen“. Only the upload half of that button: no bucket listing, nothing deleted, nothing at all
/// without trainers — a run nobody decided on must never empty the public site (second pass). The version last
/// published this way is kept in a small file in the data directory; without an R2 key nothing happens and the next
/// start tries again. New fights still go first (the worker's order, review 1.0.3 A7).
/// </summary>
public sealed class RepublishAfterUpdate(
    SitePublishWorker worker,
    ISitePublisherFactory publishers,
    AppInfo app,
    AppPaths paths,
    ILogger<RepublishAfterUpdate> log) : BackgroundService
{
    public const string FileName = "website-version.txt";

    private string MarkerPath => Path.Combine(paths.Root, FileName);

    /// <summary>Republishes if this version has not yet; public for tests, the service runs it once at start.</summary>
    /// <returns>Whether everything was queued.</returns>
    public async Task<bool> RunOnceAsync(CancellationToken ct)
    {
        if (ReadMarker() == app.Version)
        {
            return false;
        }

        if (publishers.Create() is null)
        {
            // No R2 key yet: nothing could be uploaded; the next start tries again.
            return false;
        }

        var queued = await worker.RequeueDatabaseFilesAsync(ct);
        Directory.CreateDirectory(paths.Root);
        await File.WriteAllTextAsync(MarkerPath, app.Version, ct);
        if (queued)
        {
            log.LogInformation("Version {Version} republishes the website once", app.Version);
        }

        return queued;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RunOnceAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // App shutting down; the next start tries again.
        }
        catch (Exception ex)
        {
            // The marker stays old, so the next start tries again; the bot itself is not affected.
            log.LogWarning(ex, "Republishing the website after the update failed");
        }
    }

    private string? ReadMarker()
    {
        try
        {
            return File.Exists(MarkerPath) ? File.ReadAllText(MarkerPath).Trim() : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
