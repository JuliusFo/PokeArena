using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PokeArena.Core.Data;

/// <summary>A database copy in <see cref="AppPaths.BackupsDirectory"/>.</summary>
public sealed record BackupInfo(string Path, DateTimeOffset CreatedAt);

/// <summary>
/// Copies of the live database (spec §10): on every app start before the migrations, so an update can always be
/// undone, and on demand. Uses <c>VACUUM INTO</c> — copying the file would lose what is still in the WAL. Keeps the
/// newest <see cref="Keep"/> copies. Restoring is done by hand (README): close the app, copy a backup over
/// <c>pokearena.db</c>.
/// </summary>
public sealed class DatabaseBackup(AppPaths paths, GameDatabase database, TimeProvider time, ILogger<DatabaseBackup> log)
{
    public const int Keep = 10;

    private const string Prefix = "pokearena-";
    private const string TimeFormat = "yyyyMMdd-HHmmss";

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Raised after a backup was written.</summary>
    public event Action? Changed;

    public string Directory => paths.BackupsDirectory;

    /// <summary>The newest backup, or null if there is none yet.</summary>
    public BackupInfo? Latest => List().FirstOrDefault();

    /// <summary>Writes a copy of the live database (never the dry-run sandbox) and drops the oldest copies.</summary>
    public async Task<BackupInfo> CreateAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var now = time.GetUtcNow();
            var path = Path.Combine(Directory, Prefix + now.ToString(TimeFormat, CultureInfo.InvariantCulture) + ".db");
            File.Delete(path); // VACUUM INTO refuses an existing file (two backups within one second).

            await using (var db = database.OpenLive())
            {
                // VACUUM cannot take a parameter for its target; the path is ours, quotes are escaped anyway.
#pragma warning disable EF1002
                await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{path.Replace("'", "''", StringComparison.Ordinal)}'", ct);
#pragma warning restore EF1002
            }

            foreach (var old in List().Skip(Keep))
            {
                File.Delete(old.Path);
            }

            log.LogInformation("Database backed up to {Path}", path);
            Changed?.Invoke();
            return new BackupInfo(path, now);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Database backup failed");
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>All backups, newest first.</summary>
    public IReadOnlyList<BackupInfo> List()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return [];
        }

        return [.. System.IO.Directory.EnumerateFiles(Directory, Prefix + "*.db")
            .Select(file => DateTimeOffset.TryParseExact(
                Path.GetFileNameWithoutExtension(file)[Prefix.Length..],
                TimeFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var createdAt)
                ? new BackupInfo(file, createdAt)
                : null)
            .OfType<BackupInfo>()
            .OrderByDescending(b => b.CreatedAt)];
    }
}
