using Microsoft.EntityFrameworkCore;
using PokeArena.Core.Chat;
using PokeArena.Core.Game;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests.Game;

public class DryRunTrainersTests : IDisposable
{
    private readonly GameFixture _game = new();
    private readonly ChatInbox _inbox = new();
    private readonly ChatLog _chatLog;
    private readonly DryRunTrainers _trainers;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public DryRunTrainersTests()
    {
        _chatLog = new ChatLog(_game.Time);
        _trainers = new DryRunTrainers(
            _game.Database, GameFixture.Species, GameFixture.Npcs, _game.Trainers, PvpSettings.Default, new FixedSeeds(), _inbox, _chatLog, _game.Time);
    }

    [Fact]
    public async Task Prepare_registers_a_test_viewer_with_a_full_team_in_the_sandbox()
    {
        _game.DryRun.Enabled = true;

        await PrepareAsync(" Ash ");

        await using var db = _game.Database.Open();
        var trainer = await db.Trainers.Include(t => t.Specimens).SingleAsync(_ct);
        Assert.Equal(("probelauf:ash", "ash", "Ash"), (trainer.TwitchUserId, trainer.Login, trainer.DisplayName));
        Assert.Equal(6, trainer.Specimens.Count);
        Assert.Contains(GameFixture.Species.Starters, s => s.Id == trainer.Specimens.Single(x => x.IsStarter).SpeciesId);
        Assert.Equal("Testzuschauer Ash hat jetzt 6 Pokémon.", _chatLog.Snapshot().Single().Text);
    }

    [Fact]
    public async Task Prepare_tops_up_an_existing_test_viewer_and_keeps_the_starter()
    {
        _game.DryRun.Enabled = true;
        await _game.Trainers.RegisterAsync(DryRunTrainers.UserId("Ash"), "ash", "Ash", GameFixture.Species.Get(25), _ct);

        await PrepareAsync("Ash");
        await PrepareAsync("ASH");

        await using var db = _game.Database.Open();
        var specimens = await db.Specimens.ToListAsync(_ct);
        Assert.Equal(6, specimens.Count);
        Assert.Equal(25, specimens.Single(s => s.IsStarter).SpeciesId);
    }

    [Fact]
    public async Task Unlock_gives_a_test_viewer_enough_distinct_species_and_the_achievements()
    {
        _game.DryRun.Enabled = true;
        await PrepareAsync("Ash");

        _trainers.Unlock("Ash", GameFixture.Npcs.Find("rocko")!);
        await RunQueuedAsync();

        await using var db = _game.Database.Open();
        var specimens = await db.Specimens.ToListAsync(_ct);
        Assert.Equal(25, specimens.Select(s => s.SpeciesId).Distinct().Count());
        Assert.Single(specimens, s => s.IsStarter);
        Assert.Equal([15, 25], await db.AchievementUnlocks.OrderBy(u => u.Threshold).Select(u => u.Threshold).ToListAsync(_ct));
        Assert.Equal("Testzuschauer Ash hat jetzt 25 verschiedene Pokémon und kann Rocko herausfordern.", _chatLog.Snapshot()[^1].Text);
    }

    [Fact]
    public async Task Prepare_never_touches_the_live_database()
    {
        _game.DryRun.Enabled = true;
        _trainers.Prepare("Ash");
        _game.DryRun.Enabled = false;

        await RunQueuedAsync();

        await using var db = _game.OpenLive();
        Assert.Empty(db.Trainers);
        Assert.Empty(_chatLog.Snapshot());
    }

    private async Task PrepareAsync(string name)
    {
        _trainers.Prepare(name);
        await RunQueuedAsync();
    }

    /// <summary>Runs the queued work as the command processor would.</summary>
    private async Task RunQueuedAsync()
    {
        Assert.True(_inbox.Reader.TryRead(out var item));
        await Assert.IsType<QueuedWork>(item).RunAsync(_ct);
    }

    public void Dispose() => _game.Dispose();

    private sealed class FixedSeeds : ISeedSource
    {
        public int NextSeed() => 99;
    }
}
