using System.Windows;
using PokeArena.Core;
using Serilog;
using Velopack;
using Velopack.Sources;

namespace PokeArena.Desktop.Services;

/// <summary>
/// Self-update through Velopack (WP-11, ADR-0010) for a copy installed with <c>Setup.exe</c> or unpacked from
/// <c>Portable.zip</c>. Velopack downloads into its own package folder; the data directory
/// (<c>%LOCALAPPDATA%\PokeArena</c>) is never part of the installation.
/// </summary>
public sealed class VelopackUpdater : IAppUpdater
{
    private readonly UpdateManager _manager;
    private UpdateInfo? _found;

    private VelopackUpdater(UpdateManager manager)
    {
        _manager = manager;
    }

    public bool CanSelfUpdate => true;

    /// <summary>Null if this copy was not installed by Velopack (old ZIP, developer build) and so cannot update itself.</summary>
    public static VelopackUpdater? TryCreate(AppUpdateOptions options)
    {
        try
        {
            var manager = options.Source.StartsWith(AppUpdateOptions.GitHubRepository, StringComparison.OrdinalIgnoreCase)
                ? new UpdateManager(new GithubSource(options.Source, accessToken: null, prerelease: false))
                : new UpdateManager(options.Source);
            return manager.IsInstalled ? new VelopackUpdater(manager) : null;
        }
        catch (Exception ex)
        {
            // A broken update source must never keep the bot from starting.
            Log.Warning(ex, "Updates: source {Source} could not be set up", options.Source);
            return null;
        }
    }

    public async Task<string?> FindNewerAsync(CancellationToken ct)
    {
        _found = await _manager.CheckForUpdatesAsync().WaitAsync(ct);
        return _found?.TargetFullRelease.Version.ToString();
    }

    public Task DownloadAsync(CancellationToken ct) =>
        _manager.DownloadUpdatesAsync(_found ?? throw new InvalidOperationException("No update found to download."), null, ct);

    public void RestartIntoUpdate()
    {
        var pending = _manager.UpdatePendingRestart ?? throw new InvalidOperationException("No downloaded update to install.");

        // The updater waits up to 60 s for this process to end, applies the update without dialogs (nobody may be
        // watching the server) and starts the new version with the same arguments (e.g. a test data directory).
        _manager.WaitExitThenApplyUpdates(pending, silent: true, restart: true, restartArgs: Environment.GetCommandLineArgs()[1..]);

        // The normal way out: the main window's Closing stops the host, so chat queue, uploads and log are flushed.
        Application.Current.Dispatcher.InvokeAsync(() => Application.Current.MainWindow?.Close());
    }
}
