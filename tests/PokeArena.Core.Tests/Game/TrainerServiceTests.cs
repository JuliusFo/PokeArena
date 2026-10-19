using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PokeArena.Core.Chat;
using PokeArena.Core.Data;
using PokeArena.Core.Game;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests.Game;

public class TrainerServiceTests : IDisposable
{
    private readonly GameFixture _game = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static Species Pikachu => GameFixture.Species.Get(25);

    [Fact]
    public async Task Registration_creates_the_trainer_with_a_protected_starter()
    {
        var result = await _game.Trainers.RegisterAsync("1001", "ash", "Ash", Pikachu, _ct);

        Assert.Equal(new RegistrationResult(RegistrationStatus.Registered, Pikachu), result);
        await using var db = _game.OpenLive();
        var trainer = await db.Trainers.Include(t => t.Specimens).SingleAsync(_ct);
        Assert.Equal(("1001", "Ash", _game.Time.GetUtcNow()), (trainer.TwitchUserId, trainer.DisplayName, trainer.RegisteredAt));
        var starter = Assert.Single(trainer.Specimens);
        Assert.Equal((25, true, SpecimenOrigin.Starter), (starter.SpeciesId, starter.IsStarter, starter.Origin));
    }

    [Fact]
    public async Task Second_registration_keeps_the_first_starter()
    {
        await _game.Trainers.RegisterAsync("1001", "ash", "Ash", Pikachu, _ct);

        var again = await _game.Trainers.RegisterAsync("1001", "ash", "Ash", GameFixture.Species.Get(133), _ct);

        Assert.Equal(new RegistrationResult(RegistrationStatus.AlreadyRegistered, Pikachu), again);
        await using var db = _game.OpenLive();
        Assert.Equal(1, await db.Specimens.CountAsync(_ct));
    }

    [Fact]
    public async Task Registrations_survive_a_restart()
    {
        await _game.Trainers.RegisterAsync("1001", "ash", "Ash", Pikachu, _ct);

        using var restarted = _game.NewDatabase();
        var service = new TrainerService(restarted, GameFixture.Species, _game.Time, NullLogger<TrainerService>.Instance);

        var trainer = Assert.Single(await service.ListAsync(_ct));
        Assert.Equal(new TrainerSummary("1001", "Ash", Pikachu, 1, _game.Time.GetUtcNow()), trainer);
    }

    [Fact]
    public async Task Observe_reports_registration_and_refreshes_a_changed_display_name()
    {
        Assert.False(await _game.Trainers.ObserveAsync(Message("1001", "Ash"), _ct));
        await _game.Trainers.RegisterAsync("1001", "ash", "Ash", Pikachu, _ct);
        var changes = 0;
        _game.Trainers.Changed += () => changes++;

        Assert.True(await _game.Trainers.ObserveAsync(Message("1001", "Ash"), _ct));
        Assert.True(await _game.Trainers.ObserveAsync(Message("1001", "AshKetchum"), _ct));

        await using var db = _game.OpenLive();
        var trainer = await db.Trainers.SingleAsync(_ct);
        Assert.Equal(("AshKetchum", "ashketchum"), (trainer.DisplayName, trainer.Login));
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task Observe_refreshes_a_changed_login_with_the_same_display_name()
    {
        await _game.Trainers.RegisterAsync("1001", "ash", "サトシ", Pikachu, _ct);
        Assert.True(await _game.Trainers.ObserveAsync(new ChatMessage("m", "1001", "satoshi", "サトシ", "hallo", ChatRole.Viewer), _ct));

        await using var db = _game.OpenLive();
        Assert.Equal("satoshi", (await db.Trainers.SingleAsync(_ct)).Login);
    }

    [Theory]
    [InlineData("@satoshi")]
    [InlineData("Satoshi")]
    [InlineData("サトシ")]
    public async Task Mentions_resolve_by_login_or_display_name(string mention)
    {
        await _game.Trainers.RegisterAsync("1001", "satoshi", "サトシ", Pikachu, _ct);

        Assert.Equal(new TrainerName("1001", "サトシ", "satoshi"), await _game.Trainers.FindByNameAsync(mention, _ct));
    }

    [Fact]
    public async Task Mentions_prefer_the_login_and_miss_unknown_names()
    {
        await _game.Trainers.RegisterAsync("1", "misty", "Rocko", Pikachu, _ct);
        await _game.Trainers.RegisterAsync("2", "rocko", "RockoDerEchte", Pikachu, _ct);

        Assert.Equal("2", (await _game.Trainers.FindByNameAsync("@Rocko", _ct))?.TwitchUserId);
        Assert.Null(await _game.Trainers.FindByNameAsync("@gary", _ct));
        Assert.Null(await _game.Trainers.FindByNameAsync("@", _ct));
    }

    [Fact]
    public async Task List_shows_newest_first_with_starter_and_count()
    {
        await _game.Trainers.RegisterAsync("1", "erster", "Erster", Pikachu, _ct);
        _game.Time.Advance(TimeSpan.FromMinutes(1));
        await _game.Trainers.RegisterAsync("2", "zweiter", "Zweiter", GameFixture.Species.Get(7), _ct);

        var list = await _game.Trainers.ListAsync(_ct);

        Assert.Equal(["Zweiter", "Erster"], list.Select(t => t.DisplayName));
        Assert.Equal("Schiggy", list[0].Starter.Name);
    }

    [Fact]
    public async Task Switching_the_dry_run_forgets_cached_registrations()
    {
        await _game.Trainers.RegisterAsync("1001", "ash", "Ash", Pikachu, _ct);
        Assert.True(await _game.Trainers.ObserveAsync(Message("1001", "Ash"), _ct));

        _game.DryRun.Enabled = true;

        Assert.False(await _game.Trainers.ObserveAsync(Message("1001", "Ash"), _ct));
        Assert.Empty(await _game.Trainers.ListAsync(_ct));
    }

    private static ChatMessage Message(string userId, string name) => new("m", userId, name.ToLowerInvariant(), name, "hallo", ChatRole.Viewer);

    public void Dispose() => _game.Dispose();
}
