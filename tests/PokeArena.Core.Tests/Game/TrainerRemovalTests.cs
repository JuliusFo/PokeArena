using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SitePaths = PokeArena.Contracts.SitePaths;
using PokeArena.Contracts;
using PokeArena.Core.Chat;
using PokeArena.Core.Data;
using PokeArena.Core.Game;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests.Game;

public class TrainerRemovalTests : IDisposable
{
    private readonly GameFixture _game = new();
    private readonly RecordingSender _chat = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly TrainerRemoval _removal;

    public TrainerRemovalTests()
    {
        _removal = new TrainerRemoval(
            _game.Trainers,
            new BattleService(_game.Database, GameFixture.Species, GameFixture.Npcs),
            _chat,
            _game.Time,
            NullLogger<TrainerRemoval>.Instance);
    }

    [Fact]
    public async Task Delete_removes_the_trainer_and_anonymizes_their_fights()
    {
        var battleId = await ArrangeFightAsync();

        Assert.True(await _game.Trainers.DeleteAsync("1001", _ct));

        await using var db = _game.OpenLive();
        Assert.Equal(["1002"], await db.Trainers.Select(t => t.TwitchUserId).ToListAsync(_ct));
        Assert.Equal(["1002"], await db.Specimens.Select(s => s.TrainerId).ToListAsync(_ct));
        Assert.Empty(await db.AchievementUnlocks.ToListAsync(_ct));
        Assert.Empty(await db.NpcBadges.ToListAsync(_ct));
        Assert.Equal([NpcService.CooldownKey("10010", "eich")], await db.Cooldowns.Select(c => c.Key).ToListAsync(_ct));
        Assert.Equal(["1002"], await db.BattleParticipants.Select(p => p.TrainerId).ToListAsync(_ct));

        var log = BattleLog.FromJson((await db.Battles.SingleAsync(_ct)).LogJson);
        Assert.Equal(
            [(null, BattleLog.DeletedOwner), ("1002", "Misty")],
            log.Fighters.Select(f => (f.TrainerId, f.Owner)));
        Assert.Single(log.Events);

        var queued = await db.PublishQueue.Select(i => i.Path).ToListAsync(_ct);
        Assert.Contains(SitePaths.Trainer("1001"), queued);
        Assert.Contains(SitePaths.Trainer("1002"), queued);
        Assert.Contains(SitePaths.Battle(battleId), queued);
        Assert.Contains(SitePaths.BattleIndex, queued);
        Assert.Contains(SitePaths.Leaderboard, queued);

        Assert.False(await _game.Trainers.DeleteAsync("1001", _ct));
    }

    [Fact]
    public async Task Chat_deletion_needs_a_confirmation_from_the_same_moderator()
    {
        await ArrangeFightAsync();

        await _removal.RequestAsync(Mod("9", "Rocko"), "@Ash", _ct);
        await _removal.RequestAsync(Mod("8", "Rocko2"), "@ash ja", _ct);
        Assert.True(await TrainerExistsAsync("1001"));

        await _removal.RequestAsync(Mod("9", "Rocko"), "@ash ja", _ct);

        Assert.False(await TrainerExistsAsync("1001"));
        Assert.Equal(
            "@Rocko, wirklich Ash mit allen Pokémon löschen? Das lässt sich nicht rückgängig machen. " +
            "Bestätige innerhalb von 60 Sekunden mit !entfernen @ash ja",
            _chat.Sent[0]);
        Assert.Equal("Ash wurde mit allen Pokémon gelöscht.", _chat.Sent[^1]);
    }

    [Fact]
    public async Task A_deleted_viewer_counts_as_unregistered_at_once()
    {
        await ArrangeFightAsync();
        var ash = new ChatMessage("m", "1001", "ash", "Ash", "hallo", ChatRole.Viewer);
        Assert.True(await _game.Trainers.ObserveAsync(ash, _ct));

        await _game.Trainers.DeleteAsync("1001", _ct);

        Assert.False(await _game.Trainers.ObserveAsync(ash, _ct));
    }

    [Fact]
    public async Task A_trainer_whose_login_was_released_is_confirmed_by_display_name()
    {
        await _game.Trainers.RegisterAsync("1", "bob", "Bobby", GameFixture.Species.Starters[0], _ct);
        await _game.Trainers.RegisterAsync("2", "bob", "Bob", GameFixture.Species.Starters[0], _ct);

        await _removal.RequestAsync(Mod("9", "Rocko"), "@Bobby", _ct);
        Assert.EndsWith("mit !entfernen @Bobby ja", _chat.Sent[^1]);
        await _removal.RequestAsync(Mod("9", "Rocko"), "@Bobby ja", _ct);

        Assert.False(await TrainerExistsAsync("1"));
    }

    [Fact]
    public async Task Ambiguous_or_odd_names_are_not_guessed_or_repeated()
    {
        await _game.Trainers.RegisterAsync("1", "ash_one", "Ash", GameFixture.Species.Starters[0], _ct);
        await _game.Trainers.RegisterAsync("2", "ash_two", "Ash", GameFixture.Species.Starters[0], _ct);

        await _removal.RequestAsync(Mod("9", "Rocko"), "@Ash", _ct);
        await _removal.RequestAsync(Mod("9", "Rocko"), "<b>x</b>", _ct);

        Assert.Equal(
            ["@Rocko, mehrere Trainer heißen Ash – bitte in der PokeArena-App löschen.", "@Rocko, diesen Trainer finde ich nicht."],
            _chat.Sent);
    }

    [Fact]
    public async Task A_confirmation_for_another_name_only_asks_about_that_one()
    {
        await ArrangeFightAsync();

        await _removal.RequestAsync(Mod("9", "Rocko"), "@ash", _ct);
        await _removal.RequestAsync(Mod("9", "Rocko"), "@misty ja", _ct);

        Assert.True(await TrainerExistsAsync("1001"));
        Assert.True(await TrainerExistsAsync("1002"));
        Assert.StartsWith("@Rocko, wirklich Misty", _chat.Sent[^1]);
    }

    [Fact]
    public async Task A_request_from_the_dry_run_never_confirms_a_real_deletion()
    {
        await ArrangeFightAsync();

        await _removal.RequestAsync(Mod("9", "Rocko") with { IsSimulated = true }, "@ash", _ct);
        await _removal.RequestAsync(Mod("9", "Rocko"), "@ash ja", _ct);

        Assert.True(await TrainerExistsAsync("1001"));
        Assert.StartsWith("@Rocko, wirklich Ash", _chat.Sent[^1]);
    }

    [Fact]
    public async Task An_expired_confirmation_only_asks_again()
    {
        await ArrangeFightAsync();

        await _removal.RequestAsync(Mod("9", "Rocko"), "@ash", _ct);
        _game.Time.Advance(TrainerRemoval.ConfirmationTimeout);
        await _removal.RequestAsync(Mod("9", "Rocko"), "@ash ja", _ct);

        Assert.True(await TrainerExistsAsync("1001"));
        Assert.Equal(2, _chat.Sent.Count(t => t.StartsWith("@Rocko, wirklich Ash", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Unknown_or_missing_names_get_a_hint()
    {
        await _removal.RequestAsync(Mod("9", "Rocko"), "", _ct);
        await _removal.RequestAsync(Mod("9", "Rocko"), "@niemand ja", _ct);

        Assert.Equal(
            ["@Rocko, wen soll ich löschen? Zum Beispiel: !entfernen @name", "@Rocko, ich finde keinen Trainer namens niemand."],
            _chat.Sent);
    }

    /// <summary>Ash (1001) and Misty (1002) with a PvP fight, plus Ash's achievement, badge and NPC cooldown.</summary>
    private async Task<long> ArrangeFightAsync()
    {
        await _game.Trainers.RegisterAsync("1001", "ash", "Ash", GameFixture.Species.Get(25), _ct);
        await _game.Trainers.RegisterAsync("1002", "misty", "Misty", GameFixture.Species.Get(7), _ct);
        await using var db = _game.OpenLive();
        var now = _game.Time.GetUtcNow();
        var log = new BattleLog(
            [new LogFighter(0, 25, 35, "1001", "Ash"), new LogFighter(1, 7, 44, "1002", "Misty")],
            [new BattleEvent(BattleEventKind.Fainted, 1, Fighter: 1)]);
        var battle = new Battle
        {
            Kind = BattleKind.Pvp,
            FoughtAt = now,
            Outcome = BattleOutcome.ChallengerWon,
            Rounds = 1,
            LogJson = log.ToJson(),
            Participants =
            [
                new BattleParticipant { TrainerId = "1001", Won = true },
                new BattleParticipant { TrainerId = "1002", Won = false },
            ],
        };
        db.Battles.Add(battle);
        db.AchievementUnlocks.Add(new AchievementUnlock { TrainerId = "1001", Threshold = 15, UnlockedAt = now });
        db.NpcBadges.Add(new NpcBadge { TrainerId = "1001", NpcId = "eich", WonAt = now, BattleId = 1 });
        db.Cooldowns.Add(new Cooldown { Key = NpcService.CooldownKey("1001", "eich"), Until = now });
        db.Cooldowns.Add(new Cooldown { Key = NpcService.CooldownKey("10010", "eich"), Until = now });
        db.PublishQueue.RemoveRange(db.PublishQueue);
        await db.SaveChangesAsync(_ct);
        return battle.Id;
    }

    private async Task<bool> TrainerExistsAsync(string id)
    {
        await using var db = _game.OpenLive();
        return await db.Trainers.AnyAsync(t => t.TwitchUserId == id, _ct);
    }

    private static ChatMessage Mod(string userId, string name) =>
        new(Guid.NewGuid().ToString(), userId, name.ToLowerInvariant(), name, "!entfernen", ChatRole.Moderator);

    public void Dispose() => _game.Dispose();
}
