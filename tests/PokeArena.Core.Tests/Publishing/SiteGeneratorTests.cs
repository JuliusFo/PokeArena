using PokeArena.Contracts;
using PokeArena.Core.Data;
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
