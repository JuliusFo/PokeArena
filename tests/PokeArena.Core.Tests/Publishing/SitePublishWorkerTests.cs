using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PokeArena.Contracts;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Publishing;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests.Publishing;

public class SitePublishWorkerTests : IDisposable
{
    private readonly GameFixture _game = new();
    private readonly FakeSitePublisher _bucket = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static Species Starter => GameFixture.Species.Starters[0];

    [Fact]
    public async Task Registration_publishes_the_trainer_page_and_the_leaderboard()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        var worker = Worker();

        await worker.PublishDueAsync(_ct);

        Assert.Equal([SitePaths.Meta, SitePaths.Leaderboard, SitePaths.Statistics, SitePaths.Trainer("1")], _bucket.Files.Keys.Order(StringComparer.Ordinal));
        var leaderboard = Read(SitePaths.Leaderboard, SiteJson.Default.Leaderboard);
        Assert.Equal([new LeaderboardEntry(1, "1", "Ash", 1, 1)], leaderboard.Entries);
        var trainer = Read(SitePaths.Trainer("1"), SiteJson.Default.TrainerPage);
        Assert.Equal(("Ash", Starter.Name, true), (trainer.DisplayName, trainer.Pokedex.Single().Species.Name, trainer.Pokedex.Single().HasStarter));
        Assert.Equal(SiteGenerator.MutableCache, _bucket.Files[SitePaths.Leaderboard].CacheControl);

        await using var db = _game.OpenLive();
        Assert.Empty(db.PublishQueue);
        Assert.Equal(new PublishState(true, 0, null, _game.Time.GetUtcNow()), worker.State);
    }

    [Fact]
    public async Task The_dry_run_never_reaches_the_live_outbox()
    {
        _game.DryRun.Enabled = true;
        await _game.Trainers.RegisterAsync("probelauf:a", "a", "A", Starter, _ct);

        await Worker().PublishDueAsync(_ct);

        Assert.Empty(_bucket.Calls);
        await using var db = _game.OpenLive();
        Assert.Empty(db.PublishQueue);
    }

    [Fact]
    public async Task Without_R2_access_files_wait_in_the_queue()
    {
        _bucket.IsSetUp = false;
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        var worker = Worker();

        await worker.PublishDueAsync(_ct);

        Assert.Empty(_bucket.Calls);
        Assert.Equal(new PublishState(false, 4, null, null), worker.State);

        _bucket.IsSetUp = true;
        await worker.PublishDueAsync(_ct);
        Assert.Equal(4, _bucket.Files.Count);
        Assert.Equal(0, worker.State.Pending);
    }

    [Fact]
    public async Task A_failed_upload_backs_off_and_shows_the_reason()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        _bucket.Failure = new SitePublishException("Keine Verbindung zu Cloudflare. Ist der PC online?");
        var worker = Worker();

        await worker.PublishDueAsync(_ct);

        Assert.Single(_bucket.Calls);
        Assert.Equal(("Keine Verbindung zu Cloudflare. Ist der PC online?", 4), (worker.State.LastError, worker.State.Pending));
        await using (var db = _game.OpenLive())
        {
            var failed = await db.PublishQueue.SingleAsync(i => i.Attempts == 1, _ct);
            Assert.Equal(_game.Time.GetUtcNow() + SitePublishWorker.FirstBackoff, failed.NextAttemptAt);
        }

        // Once the backoff is over, one round uploads all three.
        _bucket.Failure = null;
        _game.Time.Advance(SitePublishWorker.FirstBackoff);
        await worker.PublishDueAsync(_ct);
        Assert.Equal(4, _bucket.Files.Count);
        Assert.Null(worker.State.LastError);
    }

    [Fact]
    public async Task Unexpected_errors_get_a_generic_reason()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        _bucket.Failure = new InvalidOperationException("boom");
        var worker = Worker();

        await worker.PublishDueAsync(_ct);

        Assert.Equal("Unerwarteter Fehler beim Hochladen (Details im Log).", worker.State.LastError);
    }

    [Fact]
    public async Task A_file_queued_again_during_its_upload_is_uploaded_again()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        _bucket.OnPut = async path =>
        {
            _bucket.OnPut = null;
            await using var db = _game.OpenLive();
            await SiteOutbox.EnqueueAsync(db, [path], _game.Time.GetUtcNow(), _ct);
            await db.SaveChangesAsync(_ct);
        };
        var worker = Worker();

        await worker.PublishDueAsync(_ct);

        Assert.Equal(1, worker.State.Pending);
        await worker.PublishDueAsync(_ct);
        Assert.Equal(0, worker.State.Pending);
        Assert.Equal(5, _bucket.Calls.Count);
    }

    [Fact]
    public async Task A_deleted_trainer_is_removed_from_the_bucket()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        var worker = Worker();
        await worker.PublishDueAsync(_ct);
        await using (var db = _game.OpenLive())
        {
            await db.Specimens.ExecuteDeleteAsync(_ct);
            await db.Trainers.ExecuteDeleteAsync(_ct);
            await SiteOutbox.EnqueueAsync(db, SiteOutbox.ForTrainers(["1"]), _game.Time.GetUtcNow(), _ct);
            await db.SaveChangesAsync(_ct);
        }

        await worker.PublishDueAsync(_ct);

        Assert.Contains("delete " + SitePaths.Trainer("1"), _bucket.Calls);
        Assert.False(_bucket.Files.ContainsKey(SitePaths.Trainer("1")));
        Assert.Empty(Read(SitePaths.Leaderboard, SiteJson.Default.Leaderboard).Entries);
    }

    [Fact]
    public async Task Republishing_queues_every_file()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        await _game.Trainers.RegisterAsync("2", "misty", "Misty", Starter, _ct);
        var worker = Worker();
        await worker.PublishDueAsync(_ct);
        _bucket.Files.Clear();

        await worker.RepublishEverythingAsync(_ct);
        await worker.PublishDueAsync(_ct);

        Assert.Equal(
            [SitePaths.BattleIndex, SitePaths.Meta, SitePaths.Leaderboard, SitePaths.Statistics, SitePaths.Trainer("1"), SitePaths.Trainer("2")],
            _bucket.Files.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Republishing_includes_the_fights_and_removes_files_the_database_no_longer_knows()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        var fight = await AddFightAsync("1");
        var worker = Worker();
        await worker.PublishDueAsync(_ct);

        // An older backup was restored: trainer 9 and fight 50 exist only in the bucket.
        _bucket.Files[SitePaths.Trainer("9")] = new SiteFile([], SiteGenerator.MutableCache);
        _bucket.Files[SitePaths.Battle(50)] = new SiteFile([], SiteGenerator.MutableCache);
        _bucket.Files.TryRemove(SitePaths.Battle(fight), out _);

        await worker.RepublishEverythingAsync(_ct);
        await worker.PublishDueAsync(_ct);

        Assert.Equal(
            [SitePaths.BattleIndex, SitePaths.Battle(fight), SitePaths.Meta, SitePaths.Leaderboard, SitePaths.Statistics, SitePaths.Trainer("1")],
            _bucket.Files.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(51, await AddFightAsync("1"));
    }

    [Fact]
    public async Task Republishing_ignores_foreign_files_and_far_away_fight_ids()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        var fight = await AddFightAsync("1");
        var worker = Worker();
        await worker.PublishDueAsync(_ct);
        foreach (var key in new[] { "kampf/abc.json", "kampf/1.json.bak", "kampf/", "trainer/readme.txt", "trainer/probelauf:x.json", "kampf/9223372036854775807.json" })
        {
            _bucket.Files[key] = new SiteFile([], SiteGenerator.MutableCache);
        }

        await worker.RepublishEverythingAsync(_ct);
        await worker.PublishDueAsync(_ct);

        // Files in the bot's own format are cleaned up, everything else stays untouched and blocks nothing.
        Assert.Null(worker.State.LastError);
        Assert.Equal(0, worker.State.Pending);
        Assert.DoesNotContain("kampf/9223372036854775807.json", _bucket.Files.Keys);
        Assert.Contains("kampf/abc.json", _bucket.Files.Keys);
        Assert.Contains("trainer/probelauf:x.json", _bucket.Files.Keys);
        Assert.Equal(fight + 1, await AddFightAsync("1"));
    }

    [Fact]
    public async Task A_new_change_goes_out_before_a_full_republish()
    {
        await using (var db = _game.OpenLive())
        {
            await SiteOutbox.EnqueueAsync(db, Enumerable.Range(100, 40).Select(i => SitePaths.Trainer(i.ToString(System.Globalization.CultureInfo.InvariantCulture))), _game.Time.GetUtcNow(), _ct);
            await db.SaveChangesAsync(_ct);
        }

        _game.Time.Advance(TimeSpan.FromSeconds(1));
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);

        await Worker().PublishDueAsync(_ct);

        Assert.Contains(SitePaths.Trainer("1"), _bucket.Files.Keys);
    }

    [Fact]
    public async Task New_access_retries_waiting_files_at_once()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        _bucket.Failure = new SitePublishException("Cloudflare lehnt die Zugangsdaten ab.");
        var worker = Worker();
        await worker.PublishDueAsync(_ct);
        Assert.NotNull(worker.State.LastError);

        _bucket.Failure = null;
        _bucket.RaiseChanged();
        await worker.PublishDueAsync(_ct);

        Assert.Equal(0, worker.State.Pending);
        Assert.Null(worker.State.LastError);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(8, 640)]
    [InlineData(9, 900)]
    [InlineData(100, 900)]
    public void Backoff_doubles_up_to_fifteen_minutes(int attempts, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), SitePublishWorker.Backoff(attempts));

    private SitePublishWorker Worker() => new(
        _game.Database,
        new SiteGenerator(GameFixture.Species, GameFixture.Npcs),
        _bucket,
        _game.Time,
        NullLogger<SitePublishWorker>.Instance);

    /// <summary>Stores a minimal PvP fight of <paramref name="trainerId"/> and returns its id.</summary>
    private async Task<long> AddFightAsync(string trainerId)
    {
        await using var db = _game.OpenLive();
        var battle = new Battle
        {
            Kind = BattleKind.Pvp,
            FoughtAt = _game.Time.GetUtcNow(),
            Outcome = BattleOutcome.ChallengerWon,
            Rounds = 1,
            LogJson = new BattleLog(
                [new LogFighter(0, Starter.Id, 35, trainerId, "Ash"), new LogFighter(1, Starter.Id, 35, null, "[gelöscht]")],
                [new BattleEvent(BattleEventKind.Fainted, 1, Fighter: 1)]).ToJson(),
            Participants = [new BattleParticipant { TrainerId = trainerId, Won = true }],
        };
        db.Battles.Add(battle);
        await db.SaveChangesAsync(_ct);
        return battle.Id;
    }

    private T Read<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type) =>
        JsonSerializer.Deserialize(_bucket.Files[path].Content, type)!;

    public void Dispose() => _game.Dispose();
}
