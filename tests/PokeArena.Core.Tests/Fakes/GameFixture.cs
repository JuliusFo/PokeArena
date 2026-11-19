using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Core.Data;
using PokeArena.Core.Game;

namespace PokeArena.Core.Tests.Fakes;

/// <summary>
/// Game services on a migrated in-memory "live" database (in process, nothing on disk) with the real base data.
/// The database lives as long as the fixture.
/// </summary>
public sealed class GameFixture : IDisposable
{
    private static readonly Lazy<SpeciesCatalog> RealCatalog = new(() =>
        SpeciesCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "pokemon.json")));

    private static readonly Lazy<TypeChart> RealTypes = new(() =>
        TypeChart.Load(Path.Combine(AppContext.BaseDirectory, "data", "typen.json"), Species));

    private static readonly Lazy<NpcCatalog> RealNpcs = new(() =>
        NpcCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "npcs.json"), Species));

    private readonly SqliteConnection _keeper;

    public GameFixture()
    {
        LiveConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"live-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
        _keeper = new SqliteConnection(LiveConnectionString);
        _keeper.Open();

        Database = NewDatabase();
        Database.MigrateLiveAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        Trainers = new TrainerService(Database, Species, Time, NullLogger<TrainerService>.Instance);
    }

    public static SpeciesCatalog Species => RealCatalog.Value;

    public static TypeChart Types => RealTypes.Value;

    public static NpcCatalog Npcs => RealNpcs.Value;

    public string LiveConnectionString { get; }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 13, 18, 0, 0, TimeSpan.Zero));

    public DryRunMode DryRun { get; } = new();

    public GameDatabase Database { get; }

    public TrainerService Trainers { get; }

    /// <summary>Another <see cref="GameDatabase"/> on the same live data, as after an app restart.</summary>
    public GameDatabase NewDatabase() =>
        new(LiveConnectionString, DryRun, NullLogger<GameDatabase>.Instance);

    /// <summary>A context directly on the live data, bypassing the dry-run switch.</summary>
    public PokeArenaDb OpenLive() => new(PokeArenaDb.Options(LiveConnectionString));

    /// <summary>
    /// Makes every later insert into the website outbox fail. Every game flow queues its files in its last save, after
    /// the fight or the deletion was written: a flow that is one transaction must then leave nothing behind
    /// (review 1.8.1, C2).
    /// </summary>
    public void FailOutboxInserts()
    {
        using var db = OpenLive();
        db.Database.ExecuteSqlRaw("CREATE TRIGGER test_fail_outbox BEFORE INSERT ON PublishQueue BEGIN SELECT RAISE(ABORT, 'test: outbox refused'); END;");
    }

    public void Dispose()
    {
        Database.Dispose();
        _keeper.Dispose();
    }
}
