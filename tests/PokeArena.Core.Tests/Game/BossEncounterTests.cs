using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PokeArena.Contracts;
using PokeArena.Core.Chat;
using PokeArena.Core.Commands;
using PokeArena.Core.Data;
using PokeArena.Core.Game;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Publishing;
using PokeArena.Core.Tests.Fakes;
using PokeArena.Core.Twitch;

namespace PokeArena.Core.Tests.Game;

public class BossEncounterTests : IDisposable
{
    private const int Seed = 1234;

    /// <summary>Boss HP collapses to 1: the first hit wins, every participant survives and catches.</summary>
    private static readonly BossSettings EasyBoss = BossSettings.Default with { HpPerParticipant = 0.001 };

    /// <summary>Boss HP so high that the group always loses.</summary>
    private static readonly BossSettings HopelessBoss = BossSettings.Default with { HpPerParticipant = 10_000 };

    private readonly GameFixture _game = new();
    private readonly ChatInbox _inbox = new();
    private readonly RecordingSender _chat = new();
    private readonly StreamStatus _stream = Live();
    private SiteLinks _links = SiteLinks.None;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static SpeciesCatalog Species => GameFixture.Species;

    [Fact]
    public async Task Lobby_collects_participants_and_the_fight_resolves_when_the_countdown_ends()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty", "3", "Rocko");

        await boss.JoinAsync(Boss("1", "Ash"), _ct);
        await boss.JoinAsync(Boss("2", "Misty"), _ct);
        await boss.JoinAsync(Boss("2", "Misty"), _ct);
        await boss.JoinAsync(Boss("3", "Rocko"), _ct);
        Assert.Equal(["Ein wilder Boss nähert sich! Schreibt !boss, um mitzukämpfen – noch 2 Minuten."], _chat.Sent);
        Assert.True(boss.HasOpenLobby);

        await EndCountdownAsync();
        Assert.False(boss.HasOpenLobby);

        var expectedBoss = BossDraw.Species(Species, EasyBoss, 3, new BattleRandom(Seed));
        Assert.Equal($"Ein wildes {expectedBoss.Name}{BattleTexts.TierSuffix(expectedBoss)} erscheint! 3 Trainer kämpfen mit.", _chat.Sent[1]);
        Assert.Equal($"{expectedBoss.Name} wurde besiegt! Gefangen von: Ash, Misty, Rocko.", _chat.Sent[2]);

        await using var db = _game.OpenLive();
        var battle = await db.Battles.Include(b => b.Participants).SingleAsync(_ct);
        Assert.Equal(Seed, battle.Seed);
        Assert.Equal(expectedBoss.Id, battle.BossSpeciesId);
        Assert.Equal(BattleOutcome.BossDefeated, battle.Outcome);
        Assert.Equal(["1", "2", "3"], battle.Participants.Select(p => p.TrainerId).Order());
        Assert.All(battle.Participants, p => Assert.True(p.Won));
        Assert.NotEmpty(BattleLog.FromJson(battle.LogJson).Events);

        var caught = await db.Specimens.Where(s => s.Origin == SpecimenOrigin.Boss).ToListAsync(_ct);
        Assert.Equal(3, caught.Count);
        Assert.All(caught, s => Assert.Equal(expectedBoss.Id, s.SpeciesId));
    }

    [Fact]
    public async Task Too_few_participants_let_the_boss_escape_with_a_short_retry_cooldown()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash");

        await boss.JoinAsync(Boss("1", "Ash"), _ct);
        await EndCountdownAsync();
        Assert.Equal("Der Boss entkommt – es haben sich zu wenige Trainer getraut. Mindestens 2 müssen mit !boss mitmachen.", _chat.Sent[^1]);

        await boss.JoinAsync(Boss("1", "Ash"), _ct);
        Assert.Equal("@Ash, der nächste Boss kann in 2 Minuten gerufen werden.", _chat.Sent[^1]);

        // The short retry cooldown ends without an announcement.
        _game.Time.Advance(BossSettings.Default.FailedLobbyCooldown);
        Assert.False(_inbox.Reader.TryRead(out _));
        await boss.JoinAsync(Boss("1", "Ash"), _ct);
        Assert.StartsWith("Ein wilder Boss nähert sich!", _chat.Sent[^1]);

        await using var db = _game.OpenLive();
        Assert.Empty(db.Battles);
    }

    [Fact]
    public async Task A_countdown_that_is_not_whole_minutes_is_announced_in_seconds()
    {
        var boss = Boss(EasyBoss with { Countdown = TimeSpan.FromSeconds(90) });
        await RegisterAsync("1", "Ash");

        await boss.JoinAsync(Boss("1", "Ash"), _ct);

        Assert.Equal(["Ein wilder Boss nähert sich! Schreibt !boss, um mitzukämpfen – noch 90 Sekunden."], _chat.Sent);
    }

    [Fact]
    public async Task A_newcomer_in_the_last_ten_seconds_extends_the_countdown_by_ten_seconds()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty", "3", "Rocko");
        await boss.JoinAsync(Boss("1", "Ash"), _ct);

        // 10 s left: Misty extends to 20 s left; 5 s later Rocko is not late enough to extend again (15 s left).
        _game.Time.Advance(BossSettings.Default.Countdown - TimeSpan.FromSeconds(10));
        await boss.JoinAsync(Boss("2", "Misty"), _ct);
        _game.Time.Advance(TimeSpan.FromSeconds(5));
        await boss.JoinAsync(Boss("3", "Rocko"), _ct);

        // The original countdown ends: its work only waits for the rest.
        _game.Time.Advance(TimeSpan.FromSeconds(5));
        await RunQueuedWorkUntilAsync("Boss-Kampf");
        Assert.Single(_chat.Sent);

        _game.Time.Advance(TimeSpan.FromSeconds(9));
        Assert.False(_inbox.Reader.TryRead(out _));
        Assert.Single(_chat.Sent);

        _game.Time.Advance(TimeSpan.FromSeconds(1));
        await RunQueuedWorkUntilAsync("Boss-Kampf");
        Assert.EndsWith("3 Trainer kämpfen mit.", _chat.Sent[1]);
    }

    [Fact]
    public async Task Early_joins_and_joining_twice_do_not_extend_the_countdown()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty");
        await boss.JoinAsync(Boss("1", "Ash"), _ct);

        _game.Time.Advance(TimeSpan.FromSeconds(30));
        await boss.JoinAsync(Boss("2", "Misty"), _ct);
        _game.Time.Advance(BossSettings.Default.Countdown - TimeSpan.FromSeconds(35));
        await boss.JoinAsync(Boss("1", "Ash"), _ct);
        await boss.JoinAsync(Boss("2", "Misty"), _ct);

        _game.Time.Advance(TimeSpan.FromSeconds(5));
        await RunQueuedWorkUntilAsync("Boss-Kampf");
        Assert.EndsWith("2 Trainer kämpfen mit.", _chat.Sent[1]);
    }

    [Fact]
    public async Task Boss_reset_clears_the_cooldown()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty");
        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));

        await boss.ResetCooldownAsync(_ct);
        await boss.JoinAsync(Boss("1", "Ash"), _ct);

        Assert.Equal("Der Boss-Cooldown ist zurückgesetzt – mit !boss geht es sofort weiter.", _chat.Sent[^2]);
        Assert.StartsWith("Ein wilder Boss nähert sich!", _chat.Sent[^1]);
    }

    [Fact]
    public async Task The_chat_hears_when_the_boss_is_ready_again()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty");
        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));
        var sentAfterFight = _chat.Sent.Count;

        _game.Time.Advance(BossSettings.Default.Cooldown - TimeSpan.FromSeconds(1));
        Assert.False(_inbox.Reader.TryRead(out _));

        _game.Time.Advance(TimeSpan.FromSeconds(1));
        await RunQueuedWorkUntilAsync("Boss bereit");

        Assert.Equal([BossEncounter.ReadyAnnouncement], _chat.Sent.Skip(sentAfterFight));
    }

    [Fact]
    public async Task The_timer_of_an_earlier_cooldown_stays_silent_after_a_reset_and_a_new_fight()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty");
        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));
        await boss.ResetCooldownAsync(_ct);
        _game.Time.Advance(TimeSpan.FromMinutes(10));
        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));

        // The first fight's timer ends while the second cooldown still runs.
        _game.Time.Advance(BossSettings.Default.Cooldown - TimeSpan.FromMinutes(11));
        await RunQueuedWorkUntilAsync("Boss bereit");
        Assert.DoesNotContain(BossEncounter.ReadyAnnouncement, _chat.Sent);

        _game.Time.Advance(TimeSpan.FromMinutes(11));
        await RunQueuedWorkUntilAsync("Boss bereit");
        Assert.Single(_chat.Sent, BossEncounter.ReadyAnnouncement);
    }

    [Fact]
    public async Task No_ready_announcement_when_a_lobby_opened_first_or_during_the_dry_run()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty");
        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));

        // The timer's work waits in the queue behind a !boss that already opened the next lobby.
        _game.Time.Advance(BossSettings.Default.Cooldown);
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_ct))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var ready = Assert.IsType<QueuedWork>(await _inbox.Reader.ReadAsync(timeout.Token));
            await boss.JoinAsync(Boss("1", "Ash"), _ct);
            await ready.RunAsync(_ct);
        }

        Assert.DoesNotContain(BossEncounter.ReadyAnnouncement, _chat.Sent);

        await boss.JoinAsync(Boss("2", "Misty"), _ct);
        await EndCountdownAsync();
        _game.DryRun.Enabled = true;
        await EndCooldownAsync();
        Assert.DoesNotContain(BossEncounter.ReadyAnnouncement, _chat.Sent);
    }

    [Fact]
    public async Task No_ready_announcement_after_a_reset_or_while_the_stream_is_offline()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty");
        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));
        await boss.ResetCooldownAsync(_ct);
        await EndCooldownAsync();

        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));
        _stream.Report(false);
        await EndCooldownAsync();

        Assert.DoesNotContain(BossEncounter.ReadyAnnouncement, _chat.Sent);
    }

    [Fact]
    public async Task Cooldown_starts_when_the_encounter_ends_and_replies_are_throttled()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty");
        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));
        var sentAfterFight = _chat.Sent.Count;

        _game.Time.Advance(TimeSpan.FromMinutes(10));
        await boss.JoinAsync(Boss("1", "Ash"), _ct);
        await boss.JoinAsync(Boss("2", "Misty"), _ct);

        Assert.Equal(["@Ash, der nächste Boss kann in 20 Minuten gerufen werden."], _chat.Sent.Skip(sentAfterFight));

        _game.Time.Advance(TimeSpan.FromMinutes(20));
        await boss.JoinAsync(Boss("2", "Misty"), _ct);
        Assert.StartsWith("Ein wilder Boss nähert sich!", _chat.Sent[^1]);
    }

    [Fact]
    public async Task A_reduced_cooldown_counts_for_fights_within_its_time_and_survives_a_restart()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty");

        await BossCooldownAsync(boss, "60");
        Assert.Equal("Boss-Cooldown verkürzt: Für 1 Stunde ist der Boss schon nach 15 Minuten wieder bereit statt nach 30 Minuten.", _chat.Sent[^1]);

        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));
        var sentAfterFight = _chat.Sent.Count;
        _game.Time.Advance(TimeSpan.FromMinutes(15));
        await RunQueuedWorkUntilAsync("Boss bereit");
        Assert.Equal([BossEncounter.ReadyAnnouncement], _chat.Sent.Skip(sentAfterFight));

        // After a restart the reduction still counts: the fight ends at minute 19 of 60.
        var restarted = Boss(EasyBoss);
        await RunEncounterAsync(restarted, ("1", "Ash"), ("2", "Misty"));
        _game.Time.Advance(TimeSpan.FromMinutes(14));
        await restarted.JoinAsync(Boss("1", "Ash"), _ct);
        Assert.Equal("@Ash, der nächste Boss kann in 1 Minute gerufen werden.", _chat.Sent[^1]);

        // Once the hour is over, the normal cooldown is back.
        _game.Time.Advance(TimeSpan.FromMinutes(31));
        await RunEncounterAsync(restarted, ("1", "Ash"), ("2", "Misty"));
        _game.Time.Advance(TimeSpan.FromMinutes(29));
        await restarted.JoinAsync(Boss("1", "Ash"), _ct);
        Assert.Equal("@Ash, der nächste Boss kann in 1 Minute gerufen werden.", _chat.Sent[^1]);
    }

    [Fact]
    public async Task A_reduction_shrinks_the_running_cooldown_and_moves_its_announcement()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty");
        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));
        _game.Time.Advance(TimeSpan.FromMinutes(10));

        await BossCooldownAsync(boss, "60");
        Assert.EndsWith("statt nach 30 Minuten. Der nächste Boss kann schon in 10 Minuten gerufen werden.", _chat.Sent[^1]);

        _game.Time.Advance(TimeSpan.FromMinutes(10));
        await RunQueuedWorkUntilAsync("Boss bereit");
        Assert.Equal(BossEncounter.ReadyAnnouncement, _chat.Sent[^1]);

        // The timer of the original cooldown stays silent.
        _game.Time.Advance(TimeSpan.FromMinutes(10));
        await RunQueuedWorkUntilAsync("Boss bereit");
        Assert.Single(_chat.Sent, BossEncounter.ReadyAnnouncement);
    }

    [Fact]
    public async Task A_chosen_cooldown_replaces_the_reduction_without_growing_and_aus_ends_it()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty");

        await BossCooldownAsync(boss, "90 10");
        Assert.Equal("Boss-Cooldown verkürzt: Für 1 Stunde 30 Minuten ist der Boss schon nach 10 Minuten wieder bereit statt nach 30 Minuten.", _chat.Sent[^1]);
        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));

        // A longer reduced cooldown does not lengthen the running one.
        _game.Time.Advance(TimeSpan.FromMinutes(5));
        await BossCooldownAsync(boss, "60 20");
        Assert.Equal("Boss-Cooldown verkürzt: Für 1 Stunde ist der Boss schon nach 20 Minuten wieder bereit statt nach 30 Minuten.", _chat.Sent[^1]);

        // Ending it early keeps the running cooldown; the next fight gets the normal one.
        await BossCooldownAsync(boss, "AUS");
        Assert.Equal("Die Verkürzung ist vorbei – nach dem nächsten Kampf braucht der Boss wieder 30 Minuten.", _chat.Sent[^1]);
        _game.Time.Advance(TimeSpan.FromMinutes(5));
        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));
        _game.Time.Advance(TimeSpan.FromMinutes(29));
        await boss.JoinAsync(Boss("1", "Ash"), _ct);
        Assert.Equal("@Ash, der nächste Boss kann in 1 Minute gerufen werden.", _chat.Sent[^1]);

        await BossCooldownAsync(boss, "0");
        Assert.Equal("@Mod, der Boss-Cooldown ist gerade nicht verkürzt.", _chat.Sent[^1]);
        Assert.Empty(_chat.Hints);
    }

    [Fact]
    public async Task Renewing_the_reduction_does_not_shrink_an_already_short_cooldown_again()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty");
        await BossCooldownAsync(boss, "60");
        _game.Time.Advance(TimeSpan.FromMinutes(48));
        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));

        // The fight at minute 50 got 15 minutes; the reduction ran out at minute 60, the mod renews it at minute 61.
        _game.Time.Advance(TimeSpan.FromMinutes(11));
        await BossCooldownAsync(boss, "60");
        Assert.EndsWith("statt nach 30 Minuten.", _chat.Sent[^1]);
        await BossCooldownAsync(boss, "aus");
        await BossCooldownAsync(boss, "60");
        Assert.EndsWith("statt nach 30 Minuten.", _chat.Sent[^1]);

        _game.Time.Advance(TimeSpan.FromMinutes(3));
        await boss.JoinAsync(Boss("1", "Ash"), _ct);
        Assert.Equal("@Ash, der nächste Boss kann in 1 Minute gerufen werden.", _chat.Sent[^1]);
    }

    [Fact]
    public async Task A_running_cooldown_shrinks_by_its_own_length()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty");
        await BossCooldownAsync(boss, "60 10");
        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));

        // Running: 10 minutes. 20 and 15 are longer and leave it; 5 halves it, 4 then takes four fifths of the rest.
        await BossCooldownAsync(boss, "60 20");
        await BossCooldownAsync(boss, "60 15");
        Assert.DoesNotContain(_chat.Sent, t => t.Contains("Der nächste Boss kann schon"));
        await BossCooldownAsync(boss, "60 5");
        Assert.EndsWith("Der nächste Boss kann schon in 5 Minuten gerufen werden.", _chat.Sent[^1]);
        await BossCooldownAsync(boss, "60 4");
        Assert.EndsWith("Der nächste Boss kann schon in 4 Minuten gerufen werden.", _chat.Sent[^1]);

        _game.Time.Advance(TimeSpan.FromMinutes(4));
        await RunQueuedWorkUntilAsync("Boss bereit");
        Assert.Single(_chat.Sent, BossEncounter.ReadyAnnouncement);
    }

    [Fact]
    public async Task The_failed_lobby_retry_is_not_shortened()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash");
        await boss.JoinAsync(Boss("1", "Ash"), _ct);
        await EndCountdownAsync();

        await BossCooldownAsync(boss, "60 1");
        Assert.EndsWith("statt nach 30 Minuten.", _chat.Sent[^1]);
        await boss.JoinAsync(Boss("1", "Ash"), _ct);
        Assert.Equal("@Ash, der nächste Boss kann in 2 Minuten gerufen werden.", _chat.Sent[^1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("eine Stunde")]
    [InlineData("0 10")]
    [InlineData("-5")]
    [InlineData("1441")]
    [InlineData("60 0")]
    [InlineData("60 30")]
    [InlineData("60 10 5")]
    public async Task Invalid_boss_cooldown_arguments_get_the_usage_hint_every_time(string arguments)
    {
        var boss = Boss(EasyBoss);

        await BossCooldownAsync(boss, arguments);
        await BossCooldownAsync(boss, arguments);

        // Sent, not as a throttled hint: a mod who mistypes twice still gets an answer.
        Assert.Empty(_chat.Hints);
        Assert.Equal(2, _chat.Sent.Count);
        Assert.All(_chat.Sent, text => Assert.Equal(
            "@Mod, so geht's: !bosscooldown 60 halbiert den Boss-Cooldown für 60 Minuten, !bosscooldown 60 10 setzt ihn so lange auf 10 Minuten, "
            + "!bosscooldown aus beendet das. Dauer 1 bis 1440 Minuten, Cooldown 1 bis 29 Minuten.",
            text));
        await using var db = _game.OpenLive();
        Assert.Empty(db.BossCooldownReductions);
    }

    [Fact]
    public async Task The_longest_reduction_is_24_hours_and_the_dry_run_keeps_its_own()
    {
        var boss = Boss(EasyBoss);

        await BossCooldownAsync(boss, "1440 29");
        Assert.Equal("Boss-Cooldown verkürzt: Für 24 Stunden ist der Boss schon nach 29 Minuten wieder bereit statt nach 30 Minuten.", _chat.Sent[^1]);

        // The dry run's fresh sandbox has no reduction to end; the live one stays.
        _game.DryRun.Enabled = true;
        await BossCooldownAsync(boss, "aus", simulated: true);
        Assert.Equal("@Mod, der Boss-Cooldown ist gerade nicht verkürzt.", _chat.Sent[^1]);
        await using var db = _game.OpenLive();
        Assert.Equal(TimeSpan.FromMinutes(29), (await db.BossCooldownReductions.SingleAsync(_ct)).Cooldown);
    }

    [Fact]
    public async Task Each_participant_fields_a_species_drawn_from_their_distinct_species()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty");
        var starter = Species.Starters[0].Id;
        var digda = Species.Find("Digda")!.Id;
        int[] ash = [starter, .. Enumerable.Repeat(digda, 30)];
        int[] misty = [starter, .. Enumerable.Repeat(digda, 20), Species.Find("Arkani")!.Id, Species.Find("Lapras")!.Id];
        await using (var db = _game.Database.Open())
        {
            db.Specimens.AddRange(ash.Skip(1).Select(id => new Specimen { TrainerId = "1", SpeciesId = id, Origin = SpecimenOrigin.Boss }));
            db.Specimens.AddRange(misty.Skip(1).Select(id => new Specimen { TrainerId = "2", SpeciesId = id, Origin = SpecimenOrigin.Boss }));
            await db.SaveChangesAsync(_ct);
        }

        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));

        // Same seed, same order as the encounter: boss first, then one pick per participant in join order.
        var random = new BattleRandom(Seed);
        BossDraw.Species(Species, EasyBoss, 2, random);
        int[] expected = [BossDraw.FieldedSpecies(ash, random), BossDraw.FieldedSpecies(misty, random)];
        // Guard: with this seed Misty's pick is neither her lowest id nor a duplicate, so a rule like "lowest id" or
        // "random specimen" fails here too (review of feat/boss-pick-per-species).
        Assert.NotEqual(misty.Min(), expected[1]);
        Assert.NotEqual(digda, expected[1]);
        await using var check = _game.OpenLive();
        var log = BattleLog.FromJson((await check.Battles.SingleAsync(_ct)).LogJson);
        Assert.Equal(expected, log.Fighters.Where(f => f.TrainerId is "1" or "2").OrderBy(f => f.TrainerId).Select(f => f.SpeciesId));
    }

    [Fact]
    public async Task Nobody_catches_when_all_participants_faint()
    {
        var boss = Boss(HopelessBoss);
        await RegisterAsync("1", "Ash", "2", "Misty");

        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));

        // The result says how many HP the boss had left.
        var expectedBoss = BossDraw.Species(Species, HopelessBoss, 2, new BattleRandom(Seed));
        var maxHp = BossDraw.Fighter(expectedBoss, BattleSettings.Default, HopelessBoss, 2).MaxHp;
        var name = Regex.Escape(expectedBoss.Name);
        var match = Regex.Match(_chat.Sent[^1], $@"^{name} ist entkommen – alle Pokémon wurden besiegt\. {name} hatte noch (\d+) von {maxHp} HP\.$");
        Assert.True(match.Success, _chat.Sent[^1]);
        Assert.InRange(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), 1, maxHp - 1);
        await using var db = _game.OpenLive();
        Assert.Equal(BattleOutcome.BossEscaped, (await db.Battles.SingleAsync(_ct)).Outcome);
        Assert.Empty(db.Specimens.Where(s => s.Origin == SpecimenOrigin.Boss));
    }

    [Fact]
    public async Task A_catch_that_reaches_a_threshold_unlocks_the_achievement_once()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty");
        var expectedBoss = BossDraw.Species(Species, EasyBoss, 2, new BattleRandom(Seed));
        await GiveDistinctSpeciesAsync("1", 14, except: expectedBoss);

        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));

        Assert.Equal("Ash hat 15 verschiedene Pokémon gesammelt und kann jetzt Eich mit !challenge Eich herausfordern!", _chat.Sent[^1]);
        await using (var db = _game.OpenLive())
        {
            var unlock = await db.AchievementUnlocks.SingleAsync(_ct);
            Assert.Equal(("1", 15), (unlock.TrainerId, unlock.Threshold));
        }

        // A second catch of the same species neither re-announces nor duplicates the unlock.
        _game.Time.Advance(BossSettings.Default.Cooldown);
        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));
        Assert.Single(_chat.Sent, t => t.StartsWith("Ash hat 15 verschiedene Pokémon gesammelt", StringComparison.Ordinal));
        await using (var db = _game.OpenLive())
        {
            Assert.Equal(1, await db.AchievementUnlocks.CountAsync(_ct));
        }
    }

    [Fact]
    public async Task Dry_run_skips_cooldowns_and_drops_a_lobby_from_the_other_mode()
    {
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty");
        await boss.JoinAsync(Boss("1", "Ash"), _ct);

        _game.DryRun.Enabled = true;
        await EndCountdownAsync();
        Assert.Single(_chat.Sent);

        await RegisterAsync("probelauf:a", "A", "probelauf:b", "B");
        await RunEncounterAsync(boss, ("probelauf:a", "A"), ("probelauf:b", "B"), simulated: true);
        await RunEncounterAsync(boss, ("probelauf:a", "A"), ("probelauf:b", "B"), simulated: true);

        Assert.Equal(2, _chat.Sent.Count(t => t.Contains("wurde besiegt!")));
        await using var db = _game.OpenLive();
        Assert.Empty(db.Battles);
        Assert.Empty(db.Cooldowns);
    }

    [Fact]
    public async Task A_live_boss_result_links_the_battle_page_and_queues_its_files()
    {
        _links = new SiteLinks(new WebsiteOptions { SiteUrl = "https://arena.example.de/" });
        var boss = Boss(EasyBoss);
        await RegisterAsync("1", "Ash", "2", "Misty");

        await RunEncounterAsync(boss, ("1", "Ash"), ("2", "Misty"));

        Assert.EndsWith("wurde besiegt! Gefangen von: Ash, Misty. Zum Kampf: https://arena.example.de/kampf/1", _chat.Sent[2]);
        await using var db = _game.OpenLive();
        Assert.Equal(
            [SitePaths.BattleIndex, SitePaths.Battle(1), SitePaths.Meta, SitePaths.Leaderboard, SitePaths.Trainer("1"), SitePaths.Trainer("2")],
            await db.PublishQueue.Select(i => i.Path).OrderBy(p => p).ToListAsync(_ct));
    }

    [Fact]
    public async Task A_dry_run_boss_result_has_no_link()
    {
        _links = new SiteLinks(new WebsiteOptions { SiteUrl = "https://arena.example.de" });
        _game.DryRun.Enabled = true;
        var boss = Boss(EasyBoss);
        await RegisterAsync("probelauf:a", "A", "probelauf:b", "B");

        await RunEncounterAsync(boss, ("probelauf:a", "A"), ("probelauf:b", "B"), simulated: true);

        Assert.DoesNotContain(_chat.Sent, t => t.Contains("Zum Kampf"));
    }

    private BossEncounter Boss(BossSettings settings) => new(
        _game.Database,
        Species,
        GameFixture.Npcs,
        BattleSettings.Default,
        settings,
        new DamageCalculator(BattleSettings.Default, GameFixture.Types),
        new FixedSeeds(Seed),
        _inbox,
        _chat,
        _game.Trainers,
        new BattleService(_game.Database, Species, GameFixture.Npcs),
        _links,
        _game.DryRun,
        _stream,
        _game.Time,
        NullLogger<BossEncounter>.Instance);

    /// <summary><c>!bosscooldown</c> from a moderator, through the command as the processor would run it.</summary>
    private Task BossCooldownAsync(BossEncounter boss, string arguments, bool simulated = false)
    {
        var message = new ChatMessage(Guid.NewGuid().ToString(), "9", "mod", "Mod", $"!bosscooldown {arguments}", ChatRole.Moderator, simulated);
        return new BossCooldownCommand(boss, BossSettings.Default).ExecuteAsync(new CommandContext(message, arguments, _chat), _ct);
    }

    private async Task RunEncounterAsync(BossEncounter boss, (string Id, string Name) first, (string Id, string Name) second, bool simulated = false)
    {
        await boss.JoinAsync(Boss(first.Id, first.Name, simulated), _ct);
        await boss.JoinAsync(Boss(second.Id, second.Name, simulated), _ct);
        await EndCountdownAsync();
    }

    /// <summary>
    /// Lets the countdown elapse and runs the queued work up to the fight, as the command processor would — including
    /// a ready announcement still waiting in the queue.
    /// </summary>
    private async Task EndCountdownAsync()
    {
        _game.Time.Advance(BossSettings.Default.Countdown);
        await RunQueuedWorkUntilAsync("Boss-Kampf");
    }

    /// <summary>Lets the cooldown after a fight elapse and runs the ready announcement it queued.</summary>
    private async Task EndCooldownAsync()
    {
        _game.Time.Advance(BossSettings.Default.Cooldown);
        await RunQueuedWorkUntilAsync("Boss bereit");
    }

    private async Task RunQueuedWorkUntilAsync(string name)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        QueuedWork work;
        do
        {
            work = Assert.IsType<QueuedWork>(await _inbox.Reader.ReadAsync(timeout.Token));
            await work.RunAsync(_ct);
        }
        while (work.Name != name);
    }

    private async Task RegisterAsync(params string[] idsAndNames)
    {
        for (var i = 0; i < idsAndNames.Length; i += 2)
        {
            await _game.Trainers.RegisterAsync(idsAndNames[i], idsAndNames[i + 1].ToLowerInvariant(), idsAndNames[i + 1], Species.Starters[0], _ct);
        }
    }

    /// <summary>Tops the trainer up to <paramref name="count"/> distinct species, none of them <paramref name="except"/>.</summary>
    private async Task GiveDistinctSpeciesAsync(string trainerId, int count, Species except)
    {
        await using var db = _game.Database.Open();
        var owned = await db.Specimens.Where(s => s.TrainerId == trainerId).Select(s => s.SpeciesId).Distinct().ToListAsync(_ct);
        db.Specimens.AddRange(Species.All
            .Where(s => s.Id != except.Id && !owned.Contains(s.Id))
            .Take(count - owned.Count)
            .Select(s => new Specimen { TrainerId = trainerId, SpeciesId = s.Id, Origin = SpecimenOrigin.Boss }));
        await db.SaveChangesAsync(_ct);
    }

    private static ChatMessage Boss(string userId, string name, bool simulated = false) =>
        new(Guid.NewGuid().ToString(), userId, name.ToLowerInvariant(), name, "!boss", ChatRole.Viewer, simulated);

    private static StreamStatus Live()
    {
        var stream = new StreamStatus();
        stream.Report(true);
        return stream;
    }

    public void Dispose() => _game.Dispose();
}
