using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PokeArena.Core;

/// <summary>What the top bar shows about updates.</summary>
public enum UpdateState
{
    /// <summary>No newer version known (not checked yet, or this one is current).</summary>
    Current,

    /// <summary>A newer version exists, but this copy cannot update itself — the top bar links the download.</summary>
    Available,

    /// <summary>The newer version is being downloaded.</summary>
    Downloading,

    /// <summary>Downloaded; installed as soon as <see cref="IUpdateGate.MayInstallAutomatically"/> allows, or by hand.</summary>
    Ready,

    /// <summary>Checking, downloading or starting the install failed; the next hourly check tries again.</summary>
    Failed,
}

/// <summary>Answer to a check by hand („Nach Updates suchen“).</summary>
public enum UpdateCheckResult
{
    UpToDate,
    Newer,
    Failed,

    /// <summary>A check was running already; its result shows in <see cref="UpdateCheck.State"/>.</summary>
    Busy,
}

/// <summary>
/// Keeps the app up to date (WP-11, ADR-0010): checks <see cref="FirstCheckDelay"/> after the start and then every
/// <see cref="CheckInterval"/>, downloads a newer version at once and installs it as soon as the bot is idle
/// (<see cref="IUpdateGate"/>), checked every <see cref="InstallPoll"/>. The install restarts the app. A copy that
/// cannot update itself only learns that a newer version exists. Every failure is logged and retried at the next check.
/// </summary>
public sealed class UpdateCheck(IAppUpdater updater, IUpdateGate gate, TimeProvider time, ILogger<UpdateCheck> log) : BackgroundService
{
    public const string ReleasesPage = "https://github.com/JuliusFo/PokeArena/releases/latest";

    /// <summary>Not right at the start: after a restart during a stream, Twitch reports the stream live within seconds.</summary>
    public static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    /// <summary>How soon after the stream ends a downloaded version gets installed.</summary>
    public static readonly TimeSpan InstallPoll = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _busy = new(1, 1);
    private int _installing;

    /// <summary>Raised after every change, on the changing thread.</summary>
    public event Action? Changed;

    public UpdateState State { get; private set; } = UpdateState.Current;

    /// <summary>The newer version, e.g. "1.6.1", or null while none is known.</summary>
    public string? NewerVersion { get; private set; }

    /// <summary>A check or download is running.</summary>
    public bool Checking { get; private set; }

    /// <summary>The install was started; the app is about to close and come back as the new version.</summary>
    public bool Installing => Volatile.Read(ref _installing) == 1;

    public bool CanSelfUpdate => updater.CanSelfUpdate;

    /// <summary>„Jetzt aktualisieren“ waits while this is true.</summary>
    public bool LobbyOpen => gate.LobbyOpen;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstCheckDelay, time, stoppingToken);
            var nextCheck = time.GetUtcNow();
            while (true)
            {
                if (time.GetUtcNow() >= nextCheck)
                {
                    await CheckNowAsync(stoppingToken);
                    nextCheck = time.GetUtcNow() + CheckInterval;
                }

                if (State == UpdateState.Ready && gate.MayInstallAutomatically && Install())
                {
                    return;
                }

                await Task.Delay(InstallPoll, time, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // App shutting down.
        }
    }

    /// <summary>Checks now and downloads a newer version; the hourly loop and the button share it.</summary>
    public async Task<UpdateCheckResult> CheckNowAsync(CancellationToken ct)
    {
        if (!await _busy.WaitAsync(0, ct))
        {
            return UpdateCheckResult.Busy;
        }

        var stateBefore = State;
        var versionBefore = NewerVersion;
        try
        {
            Checking = true;
            Changed?.Invoke();

            var found = await updater.FindNewerAsync(ct);
            if (found is null)
            {
                // A version downloaded earlier stays ready even if the release was withdrawn meanwhile.
                if (State != UpdateState.Ready)
                {
                    State = UpdateState.Current;
                    NewerVersion = null;
                }

                return UpdateCheckResult.UpToDate;
            }

            if (found == NewerVersion && State is UpdateState.Ready or UpdateState.Available)
            {
                return UpdateCheckResult.Newer;
            }

            NewerVersion = found;
            if (!updater.CanSelfUpdate)
            {
                State = UpdateState.Available;
                log.LogInformation("Update check: version {Version} is available (this copy cannot update itself)", found);
                return UpdateCheckResult.Newer;
            }

            State = UpdateState.Downloading;
            Changed?.Invoke();
            log.LogInformation("Update check: downloading version {Version}", found);
            await updater.DownloadAsync(ct);
            State = UpdateState.Ready;
            log.LogInformation("Update check: version {Version} is ready to install", found);
            return UpdateCheckResult.Newer;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Update check failed");
            if (stateBefore == UpdateState.Ready)
            {
                // The earlier download is still there and still gets installed.
                State = UpdateState.Ready;
                NewerVersion = versionBefore;
            }
            else if (updater.CanSelfUpdate)
            {
                State = UpdateState.Failed;
            }
            else
            {
                // Only a hint: offline or a rate limit just means no new hint.
                State = stateBefore;
                NewerVersion = versionBefore;
            }

            return UpdateCheckResult.Failed;
        }
        finally
        {
            Checking = false;
            _busy.Release();
            Changed?.Invoke();
        }
    }

    /// <summary>„Jetzt aktualisieren“: installs the downloaded version now, even during a stream — not while a boss lobby is open.</summary>
    public bool InstallNow() => State == UpdateState.Ready && !gate.LobbyOpen && Install();

    private bool Install()
    {
        if (Interlocked.Exchange(ref _installing, 1) == 1)
        {
            return false;
        }

        log.LogInformation("Installing version {Version}; PokeArena restarts", NewerVersion);
        Changed?.Invoke();
        try
        {
            updater.RestartIntoUpdate();
            return true;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Starting the install of version {Version} failed", NewerVersion);
            State = UpdateState.Failed;
            Volatile.Write(ref _installing, 0);
            Changed?.Invoke();
            return false;
        }
    }
}
