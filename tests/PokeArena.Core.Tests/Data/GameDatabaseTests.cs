using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;
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
    public async Task Seen_species_migration_fills_in_every_fighter_of_the_fights_fought_so_far()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(_ct);
        await using var db = new PokeArenaDb(new DbContextOptionsBuilder<PokeArenaDb>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261111084203_TrainerProfileImage", _ct);
        foreach (var (id, name) in new[] { ("1", "Ash"), ("2", "Misty"), ("3", "Rocko") })
        {
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"Trainers\" (\"TwitchUserId\", \"DisplayName\", \"Login\", \"RegisteredAt\", \"Tokens\") VALUES ({id}, {name}, {name.ToLowerInvariant()}, 0, 0)", _ct);
        }

        // A PvP fight Ash vs Misty, written by the real serializer; a fighter of a deleted trainer has no id but counts.
        var log = new BattleLog(
            [new LogFighter(0, 25, 70, "1", "Ash"), new LogFighter(0, 10, 90, null, BattleLog.DeletedOwner), new LogFighter(1, 129, 40, "2", "Misty")],
            []);
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO \"Battles\" (\"Id\", \"Kind\", \"FoughtAt\", \"Seed\", \"Outcome\", \"Rounds\", \"LogJson\") VALUES (1, 'Pvp', 0, 0, 'ChallengerWon', 1, {log.ToJson()})", _ct);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"BattleParticipants\" (\"BattleId\", \"TrainerId\", \"Won\") VALUES (1, '1', 1), (1, '2', 0)", _ct);

        await migrator.MigrateAsync(cancellationToken: _ct);

        Assert.Equal(
            [("1", 10), ("1", 25), ("1", 129), ("2", 10), ("2", 25), ("2", 129)],
            (await db.SeenSpecies.ToListAsync(_ct)).Select(s => (s.TrainerId, s.SpeciesId)).Order());
    }

    [Fact]
    public async Task Seen_species_dates_migration_finds_the_first_and_latest_fight_of_every_row()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(_ct);
        await using var db = new PokeArenaDb(new DbContextOptionsBuilder<PokeArenaDb>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261123125843_SeenSpecies", _ct);
        foreach (var (id, name) in new[] { ("1", "Ash"), ("2", "Misty") })
        {
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"Trainers\" (\"TwitchUserId\", \"DisplayName\", \"Login\", \"RegisteredAt\", \"Tokens\") VALUES ({id}, {name}, {name.ToLowerInvariant()}, 0, 0)", _ct);
        }

        // Three fights out of id order: fight 3 is the earliest. Pikachu fights in all, Karpador only in fight 2, which
        // Misty missed. Fights 1 and 4 share a moment: the higher id is the latest.
        var start = new DateTimeOffset(2026, 11, 1, 18, 0, 0, TimeSpan.Zero);
        var pikachu = new LogFighter(0, 25, 70, "1", "Ash");
        var karpador = new LogFighter(1, 129, 40, null, "Boss");
        foreach (var (battle, hour, fighters) in new[] { (1L, 2, new[] { pikachu }), (2L, 3, new[] { pikachu, karpador }), (3L, 1, new[] { pikachu }), (4L, 2, new[] { pikachu }) })
        {
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO \"Battles\" (\"Id\", \"Kind\", \"FoughtAt\", \"Seed\", \"Outcome\", \"Rounds\", \"LogJson\") VALUES ({battle}, 'Boss', {start.AddHours(hour).UtcTicks}, 0, 'BossEscaped', 1, {new BattleLog(fighters, []).ToJson()})", _ct);
            await db.Database.ExecuteSqlAsync($"INSERT INTO \"BattleParticipants\" (\"BattleId\", \"TrainerId\", \"Won\") VALUES ({battle}, '1', 0)", _ct);
            if (battle != 2)
            {
                await db.Database.ExecuteSqlAsync($"INSERT INTO \"BattleParticipants\" (\"BattleId\", \"TrainerId\", \"Won\") VALUES ({battle}, '2', 0)", _ct);
            }
        }

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"SeenSpecies\" (\"TrainerId\", \"SpeciesId\") VALUES ('1', 25), ('1', 129), ('2', 25), ('2', 7)", _ct);

        await migrator.MigrateAsync(cancellationToken: _ct);

        DateTimeOffset? At(int hour) => start.AddHours(hour);
        Assert.Equal(
            [
                ("1", 25, At(1), (long?)3, At(3), (long?)2),
                ("1", 129, At(3), 2, At(3), 2),
                ("2", 7, null, null, null, null), // no fight shows it: stays empty
                ("2", 25, At(1), 3, At(2), 4),
            ],
            (await db.SeenSpecies.ToListAsync(_ct))
                .Select(s => (s.TrainerId, s.SpeciesId, s.FirstSeenAt, s.FirstBattleId, s.LastSeenAt, s.LastBattleId))
                .OrderBy(s => s.TrainerId).ThenBy(s => s.SpeciesId));
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
