using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PokeArena.Core.Data;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests.Data;

public class GameDatabaseTests : IDisposable
{
    private readonly GameFixture _game = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrations_cover_the_whole_model()
    {
        await using var db = _game.OpenLive();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync(_ct));
        Assert.False(db.Database.HasPendingModelChanges(), "Model changed without a migration: run dotnet ef migrations add");
    }

    [Fact]
    public async Task Login_migration_prefills_the_lower_cased_display_name()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(_ct);
        await using var db = new PokeArenaDb(new DbContextOptionsBuilder<PokeArenaDb>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261015192508_BossEncounters", _ct);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"Trainers\" (\"TwitchUserId\", \"DisplayName\", \"RegisteredAt\") VALUES ('1001', 'AshKetchum', 0)", _ct);

        await migrator.MigrateAsync(cancellationToken: _ct);

        Assert.Equal("ashketchum", (await db.Trainers.SingleAsync(_ct)).Login);
    }

    [Fact]
    public async Task A_trainer_cannot_have_two_starters()
    {
        await using var db = _game.OpenLive();
        db.Trainers.Add(new Trainer
        {
            TwitchUserId = "1001",
            DisplayName = "Ash",
            Login = "ash",
            Specimens =
            [
                new Specimen { TrainerId = "1001", SpeciesId = 25, IsStarter = true },
                new Specimen { TrainerId = "1001", SpeciesId = 133, IsStarter = true },
            ],
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(_ct));
    }

    [Fact]
    public async Task Timestamps_round_trip_as_utc_and_sort_in_queries()
    {
        var early = new DateTimeOffset(2026, 10, 13, 20, 0, 0, TimeSpan.FromHours(2));
        await using (var db = _game.OpenLive())
        {
            db.Trainers.Add(new Trainer { TwitchUserId = "1", DisplayName = "Spät", Login = "spaet", RegisteredAt = early.AddMinutes(5) });
            db.Trainers.Add(new Trainer { TwitchUserId = "2", DisplayName = "Früh", Login = "frueh", RegisteredAt = early });
            await db.SaveChangesAsync(_ct);
        }

        await using var read = _game.OpenLive();
        var ordered = await read.Trainers.Where(t => t.RegisteredAt >= early).OrderBy(t => t.RegisteredAt).ToListAsync(_ct);

        Assert.Equal(["Früh", "Spät"], ordered.Select(t => t.DisplayName));
        Assert.Equal(early, ordered[0].RegisteredAt);
        Assert.Equal(TimeSpan.Zero, ordered[0].RegisteredAt.Offset);
    }

    [Fact]
    public async Task Dry_run_works_on_a_fresh_sandbox_and_never_touches_live_data()
    {
        var switches = 0;
        _game.Database.Switched += () => switches++;

        _game.DryRun.Enabled = true;
        Assert.True(_game.Database.IsSandbox);
        await using (var sandbox = _game.Database.Open())
        {
            sandbox.Trainers.Add(new Trainer { TwitchUserId = "sim", DisplayName = "Test", Login = "test" });
            await sandbox.SaveChangesAsync(_ct);
        }

        _game.DryRun.Enabled = false;
        _game.DryRun.Enabled = true;
        await using (var fresh = _game.Database.Open())
        {
            Assert.Empty(await fresh.Trainers.ToListAsync(_ct));
        }

        _game.DryRun.Enabled = false;
        await using var live = _game.Database.Open();
        Assert.False(_game.Database.IsSandbox);
        Assert.Empty(await live.Trainers.ToListAsync(_ct));
        Assert.Equal(4, switches);
    }

    public void Dispose() => _game.Dispose();
}
