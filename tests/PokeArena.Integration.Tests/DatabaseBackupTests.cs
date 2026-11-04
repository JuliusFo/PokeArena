using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Core;
using PokeArena.Core.Data;

namespace PokeArena.Integration.Tests;

/// <summary>
/// Backups of a real database file in a temporary folder. A file, not an in-memory database: <c>VACUUM INTO</c> from
/// an in-memory database writes the copy into memory too.
/// </summary>
public class DatabaseBackupTests : IDisposable
{
    private readonly AppPaths _paths = new(Path.Combine(Path.GetTempPath(), "pokearena-backup-" + Guid.NewGuid().ToString("N")));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 4, 18, 0, 0, TimeSpan.Zero));
    private readonly DryRunMode _dryRun = new();
    private readonly GameDatabase _database;
    private readonly DatabaseBackup _backup;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public DatabaseBackupTests()
    {
        Directory.CreateDirectory(_paths.Root);
        _database = new GameDatabase(GameDatabase.FileConnectionString(_paths.DatabasePath), _dryRun, NullLogger<GameDatabase>.Instance);
        _database.MigrateLiveAsync(_ct).GetAwaiter().GetResult();
        _backup = new DatabaseBackup(_paths, _database, _time, NullLogger<DatabaseBackup>.Instance);
    }

    [Fact]
    public async Task Backup_is_a_readable_copy_of_the_live_database_even_during_the_dry_run()
    {
        await using (var db = _database.Open())
        {
            db.Trainers.Add(new Trainer { TwitchUserId = "1001", DisplayName = "Ash", Login = "ash" });
            await db.SaveChangesAsync(_ct);
        }

        _dryRun.Enabled = true;

        var backup = await _backup.CreateAsync(_ct);

        Assert.Equal(_time.GetUtcNow(), backup.CreatedAt);
        Assert.Equal(backup, _backup.Latest);
        await using var copy = new PokeArenaDb(PokeArenaDb.Options(GameDatabase.FileConnectionString(backup.Path)));
        Assert.Equal(["Ash"], await copy.Trainers.Select(t => t.DisplayName).ToListAsync(_ct));
    }

    [Fact]
    public async Task Only_the_newest_copies_are_kept()
    {
        for (var i = 0; i < DatabaseBackup.Keep + 2; i++)
        {
            await _backup.CreateAsync(_ct);
            _time.Advance(TimeSpan.FromMinutes(1));
        }

        var backups = _backup.List();
        Assert.Equal(DatabaseBackup.Keep, backups.Count);
        Assert.Equal(_time.GetUtcNow() - TimeSpan.FromMinutes(1), backups[0].CreatedAt);
    }

    public void Dispose()
    {
        _database.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_paths.Root, recursive: true);
    }
}
