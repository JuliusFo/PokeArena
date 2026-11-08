using System.Text.Json;
using PokeArena.Contracts;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Publishing;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests.Publishing;

public class SiteGeneratorTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 18, 0, 0, TimeSpan.Zero);

    private readonly GameFixture _game = new();
    private readonly SiteGenerator _generator = new(GameFixture.Species, GameFixture.Npcs);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_leaderboard_ranks_distinct_species_then_all_specimens()
    {
        await AddTrainerAsync("1", "Few", (1, 0), (2, 0));
        await AddTrainerAsync("2", "Many", (1, 0), (2, 0), (3, 0));
        await AddTrainerAsync("3", "Duplicates", (1, 0), (2, 0), (2, 0));

        var leaderboard = await LeaderboardAsync();

        Assert.Equal(
            [new LeaderboardEntry(1, "2", "Many", 3, 3), new LeaderboardEntry(2, "3", "Duplicates", 2, 3), new LeaderboardEntry(3, "1", "Few", 2, 2)],
            leaderboard.Entries);
        Assert.Equal(3, leaderboard.PlayerCount);
    }

    [Fact]
    public async Task The_overlay_shows_the_top_five_of_the_leaderboard()
    {
        for (var i = 1; i <= 7; i++)
        {
            // Trainer i owns i distinct species, so the order is 7, 6, 5 …
            await AddTrainerAsync(i.ToString(System.Globalization.CultureInfo.InvariantCulture), $"T{i}", [.. Enumerable.Range(1, i).Select(s => (s, 0))]);
        }

        await using var db = _game.OpenLive();
        var overlay = await _generator.OverlayAsync(db, Start, _ct);

        Assert.Equal(["T7", "T6", "T5", "T4", "T3"], overlay.Top.Select(e => e.DisplayName));
        Assert.Null(overlay.LatestBattle);
    }

    [Fact]
    public async Task Ties_go_to_whoever_completed_their_current_species_first()
    {
        // Early had species 3 first, but lost it and won it back at minute 30: their set is complete since then.
        await AddTrainerAsync("1", "Early", (1, 0), (2, 1), (3, 30));
        await AddTrainerAsync("2", "Steady", (1, 0), (2, 5), (3, 10));

        var leaderboard = await LeaderboardAsync();

        Assert.Equal(["Steady", "Early"], leaderboard.Entries.Select(e => e.DisplayName));
    }

    [Fact]
    public async Task A_duplicate_bought_later_does_not_move_the_attainment()
    {
        await AddTrainerAsync("1", "A", (1, 0), (2, 20), (2, 40));
        await AddTrainerAsync("2", "B", (1, 0), (2, 30), (1, 35));

        var leaderboard = await LeaderboardAsync();

        Assert.Equal(["A", "B"], leaderboard.Entries.Select(e => e.DisplayName));
    }

    [Fact]
    public async Task A_team_fight_page_shows_hp_left_fainted_and_not_sent_in()
    {
        // Ash's Pikachu beats Misty's only Karpador after one hit taken; Ash's Raupy never enters.
        var log = new BattleLog(
            [new LogFighter(0, 25, 70, "1", "Ash"), new LogFighter(0, 10, 90, "1", "Ash"), new LogFighter(1, 129, 40, "2", "Misty")],
            [
                new BattleEvent(BattleEventKind.SentOut, 1, Fighter: 0),
                new BattleEvent(BattleEventKind.SentOut, 1, Fighter: 2),
                new BattleEvent(BattleEventKind.Attack, 1, Fighter: 2, FighterHp: 40, Target: 0, TargetHp: 52, Damage: 18),
                new BattleEvent(BattleEventKind.Attack, 1, Fighter: 0, FighterHp: 52, Target: 2, TargetHp: 0, Damage: 45),
                new BattleEvent(BattleEventKind.Fainted, 1, Fighter: 2),
            ]);

        var page = await BattlePageAsync(new Battle { Kind = BattleKind.Pvp, FoughtAt = Start, Outcome = BattleOutcome.ChallengerWon, LogJson = log.ToJson() });

        Assert.Equal(
            [("Pikachu", (int?)52, (bool?)true), ("Raupy", 90, false), ("Karpador", 0, true)],
            page.Sides.SelectMany(s => s.Fighters).Select(f => (f.Species.Name, f.HpLeft, f.Used)));
    }

    [Fact]
    public async Task In_a_boss_fight_everyone_takes_part_from_the_start()
    {
        var log = new BattleLog(
            [new LogFighter(0, 25, 70, "1", "Ash"), new LogFighter(0, 10, 90, "2", "Misty"), new LogFighter(1, 59, 400, null, "Boss")],
            [
                new BattleEvent(BattleEventKind.Attack, 1, Fighter: 0, FighterHp: 70, Target: 2, TargetHp: 380, Damage: 20),
                new BattleEvent(BattleEventKind.Attack, 1, Fighter: 2, FighterHp: 380, Target: 0, TargetHp: 0, Damage: 90),
                new BattleEvent(BattleEventKind.Fainted, 1, Fighter: 0),
            ]);

        var page = await BattlePageAsync(new Battle
        {
            Kind = BattleKind.Boss, FoughtAt = Start, BossSpeciesId = 59, Outcome = BattleOutcome.BossEscaped, LogJson = log.ToJson(),
        });

        Assert.Equal(
            [("Pikachu", (int?)0, (bool?)true), ("Raupy", 90, true), ("Arkani", 380, true)],
            page.Sides.SelectMany(s => s.Fighters).Select(f => (f.Species.Name, f.HpLeft, f.Used)));
    }

    [Fact]
    public async Task Unknown_paths_are_rejected() =>
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await using var db = _game.OpenLive();
            await _generator.RenderAsync(db, "anderes.json", Start, _ct);
        });

    [Fact]
    public void Site_paths_round_trip()
    {
        Assert.Equal("trainer/123.json", SitePaths.Trainer("123"));
        Assert.Equal("123", SitePaths.TrainerIdOf("trainer/123.json"));
        Assert.Equal(42, SitePaths.BattleIdOf(SitePaths.Battle(42)));
        Assert.Null(SitePaths.BattleIdOf("kampf/x.json"));
        Assert.Null(SitePaths.TrainerIdOf(SitePaths.Leaderboard));
    }

    private async Task<BattlePage> BattlePageAsync(Battle battle)
    {
        await using var db = _game.OpenLive();
        db.Battles.Add(battle);
        await db.SaveChangesAsync(_ct);
        var file = await _generator.RenderAsync(db, SitePaths.Battle(battle.Id), Start, _ct);
        return JsonSerializer.Deserialize(file!.Content, SiteJson.Default.BattlePage)!;
    }

    private async Task<Leaderboard> LeaderboardAsync()
    {
        await using var db = _game.OpenLive();
        return await _generator.LeaderboardAsync(db, Start, _ct);
    }

    /// <param name="specimens">Species id and the minute after <see cref="Start"/> the trainer got it.</param>
    private async Task AddTrainerAsync(string id, string name, params (int Species, int Minute)[] specimens)
    {
        await using var db = _game.OpenLive();
        db.Trainers.Add(new Trainer
        {
            TwitchUserId = id,
            DisplayName = name,
            Login = name.ToLowerInvariant(),
            Specimens = [.. specimens.Select(s => new Specimen { TrainerId = id, SpeciesId = s.Species, AcquiredAt = Start.AddMinutes(s.Minute) })],
        });
        await db.SaveChangesAsync(_ct);
    }

    public void Dispose() => _game.Dispose();
}
