using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PokeArena.Contracts;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Publishing;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests.Publishing;

/// <summary>„Alles neu veröffentlichen“ once per new version, without anyone clicking (review 1.8.1, A7).</summary>
public sealed class RepublishAfterUpdateTests : IDisposable
{
    private readonly GameFixture _game = new();
    private readonly FakeSitePublisher _bucket = new();
    private readonly AppPaths _paths = new(Path.Combine(Path.GetTempPath(), $"pokearena-test-{Guid.NewGuid():N}"));
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static Species Starter => GameFixture.Species.Starters[0];

    [Fact]
    public async Task A_new_version_republishes_once()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        await ClearQueueAsync();

        Assert.True(await Republisher("1.8.2").RunOnceAsync(_ct));
        Assert.Contains(SitePaths.Trainer("1"), await QueuedAsync());

        // The next start of the same version does nothing.
        await ClearQueueAsync();
        Assert.False(await Republisher("1.8.2").RunOnceAsync(_ct));
        Assert.Empty(await QueuedAsync());

        // The next version does it again.
        Assert.True(await Republisher("1.8.3").RunOnceAsync(_ct));
        Assert.Contains(SitePaths.Trainer("1"), await QueuedAsync());
    }

    [Fact]
    public async Task The_automatic_run_only_uploads_and_never_cleans_the_bucket()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        _bucket.Files[SitePaths.Trainer("9")] = new SiteFile([], SiteGenerator.MutableCache);
        _bucket.Files[SitePaths.Battle(50)] = new SiteFile([], SiteGenerator.MutableCache);

        var republisher = Republisher("1.8.2");
        Assert.True(await republisher.RunOnceAsync(_ct));
        await Worker().PublishDueAsync(_ct);

        // Files only the bucket knows stay: a wrong data directory must not empty the site (review 1.8.1, second pass).
        Assert.DoesNotContain(_bucket.Calls, c => c.StartsWith("list ", StringComparison.Ordinal) || c.StartsWith("delete ", StringComparison.Ordinal));
        Assert.Contains(SitePaths.Trainer("9"), _bucket.Files.Keys);
        Assert.Contains(SitePaths.Battle(50), _bucket.Files.Keys);
        Assert.Contains(SitePaths.Trainer("1"), _bucket.Files.Keys);
    }

    [Fact]
    public async Task An_empty_database_publishes_nothing()
    {
        // A fresh or wrong data directory: an empty leaderboard must not replace the real one.
        Assert.False(await Republisher("1.8.2").RunOnceAsync(_ct));

        Assert.Empty(await QueuedAsync());
    }

    [Fact]
    public async Task A_failed_run_keeps_the_bot_starting_and_is_tried_again_at_the_next_start()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        await ClearQueueAsync();
        _game.FailOutboxInserts();
        var republisher = Republisher("1.8.2");

        await republisher.StartAsync(_ct);
        await republisher.ExecuteTask!;

        // The error is logged, not thrown, and no marker says it was done (review 1.8.1, second pass).
        Assert.False(File.Exists(Path.Combine(_paths.Root, RepublishAfterUpdate.FileName)));
        await using (var db = _game.OpenLive())
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_fail_outbox;", _ct);
        }

        Assert.True(await Republisher("1.8.2").RunOnceAsync(_ct));
    }

    [Fact]
    public async Task Without_an_r2_key_it_waits_for_a_later_start()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        _bucket.IsSetUp = false;
        Assert.False(await Republisher("1.8.2").RunOnceAsync(_ct));
        Assert.False(File.Exists(Path.Combine(_paths.Root, RepublishAfterUpdate.FileName)));

        _bucket.IsSetUp = true;
        Assert.True(await Republisher("1.8.2").RunOnceAsync(_ct));
    }

    private SitePublishWorker Worker() =>
        new(_game.Database, new SiteGenerator(GameFixture.Species, GameFixture.Npcs), _bucket, _game.Time, NullLogger<SitePublishWorker>.Instance);

    private RepublishAfterUpdate Republisher(string version) => new(
        Worker(),
        _bucket,
        new AppInfo(version),
        _paths,
        NullLogger<RepublishAfterUpdate>.Instance);

    private async Task ClearQueueAsync()
    {
        await using var db = _game.OpenLive();
        db.PublishQueue.RemoveRange(db.PublishQueue);
        await db.SaveChangesAsync(_ct);
    }

    private async Task<List<string>> QueuedAsync()
    {
        await using var db = _game.OpenLive();
        return [.. db.PublishQueue.Select(i => i.Path)];
    }

    public void Dispose()
    {
        _game.Dispose();
        if (Directory.Exists(_paths.Root))
        {
            Directory.Delete(_paths.Root, recursive: true);
        }
    }
}
