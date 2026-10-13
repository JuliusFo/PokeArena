using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PokeArena.Core.Data;

/// <summary>
/// Hands out database contexts: the live database normally, a sandbox during the dry run. The sandbox is an
/// in-memory SQLite database that is created fresh each time the dry run is switched on and dropped when it is
/// switched off — real player data is never touched by a test (WP-02 decision).
/// </summary>
public sealed class GameDatabase : IDisposable
{
    private readonly string _liveConnectionString;
    private readonly DryRunMode _dryRun;
    private readonly ILogger<GameDatabase> _log;
    private readonly Lock _lock = new();
    private SqliteConnection? _sandboxKeeper;
    private string? _sandboxConnectionString;

    public GameDatabase(string liveConnectionString, DryRunMode dryRun, ILogger<GameDatabase> log)
    {
        _liveConnectionString = liveConnectionString;
        _dryRun = dryRun;
        _log = log;
        _dryRun.Changed += OnDryRunChanged;
        if (_dryRun.Enabled)
        {
            OpenSandbox();
        }
    }

    /// <summary>Raised after switching between live and sandbox database; lists and caches must reload.</summary>
    public event Action? Switched;

    /// <summary>True while contexts point at the dry-run sandbox.</summary>
    public bool IsSandbox
    {
        get
        {
            lock (_lock)
            {
                return _sandboxConnectionString is not null;
            }
        }
    }

    public static string FileConnectionString(string path) =>
        new SqliteConnectionStringBuilder { DataSource = path }.ToString();

    /// <summary>A new context on the current database; dispose it after the unit of work.</summary>
    public PokeArenaDb Open()
    {
        string connectionString;
        lock (_lock)
        {
            connectionString = _sandboxConnectionString ?? _liveConnectionString;
        }

        return new PokeArenaDb(PokeArenaDb.Options(connectionString));
    }

    /// <summary>Creates or upgrades the live database. Runs once at startup, before any command.</summary>
    public async Task MigrateLiveAsync(CancellationToken ct)
    {
        await using var db = new PokeArenaDb(PokeArenaDb.Options(_liveConnectionString));
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count > 0)
        {
            _log.LogInformation("Applying database migrations: {Migrations}", string.Join(", ", pending));
        }

        await db.Database.MigrateAsync(ct);

        // WAL lets the UI read while a command writes; the setting is stored in the file.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);
    }

    public void Dispose()
    {
        _dryRun.Changed -= OnDryRunChanged;
        lock (_lock)
        {
            CloseSandbox();
        }
    }

    private void OnDryRunChanged()
    {
        if (_dryRun.Enabled)
        {
            OpenSandbox();
        }
        else
        {
            lock (_lock)
            {
                CloseSandbox();
            }

            _log.LogInformation("Dry run off, back on the live database");
        }

        Switched?.Invoke();
    }

    private void OpenSandbox()
    {
        // A unique name per dry run: a context still open on the previous sandbox keeps that one alive, but never
        // leaks its data into the new one.
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"probelauf-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        // The shared in-memory database lives as long as one connection to it is open.
        var keeper = new SqliteConnection(connectionString);
        keeper.Open();
        using (var db = new PokeArenaDb(PokeArenaDb.Options(connectionString)))
        {
            db.Database.Migrate();
        }

        lock (_lock)
        {
            CloseSandbox();
            _sandboxKeeper = keeper;
            _sandboxConnectionString = connectionString;
        }

        _log.LogInformation("Dry run on, using a fresh sandbox database");
    }

    private void CloseSandbox()
    {
        _sandboxKeeper?.Dispose();
        _sandboxKeeper = null;
        _sandboxConnectionString = null;
    }
}
