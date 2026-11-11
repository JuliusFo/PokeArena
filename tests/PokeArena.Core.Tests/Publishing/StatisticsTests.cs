using System.Globalization;
using System.Text.Json;
using PokeArena.Contracts;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Publishing;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests.Publishing;

/// <summary><c>statistik.json</c> (feat/statistics-page).</summary>
public class StatisticsTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 18, 0, 0, TimeSpan.Zero);

    private readonly GameFixture _game = new();
    private readonly SiteGenerator _generator = new(GameFixture.Species, GameFixture.Npcs);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private int _minute;

    [Fact]
    public async Task An_empty_game_has_empty_lists()
    {
        var stats = await StatisticsAsync();

        Assert.Equal(new CommunityTotals(0, 151, 0, 0, 0), stats.Community);
        Assert.Empty(stats.BossParticipations);
        Assert.Empty(stats.PvpFights);
        Assert.Empty(stats.BadLuck);
        Assert.Equal(0, stats.SingleOwnedCount);
        Assert.Empty(stats.Rivals);
        Assert.Empty(stats.BossesSeen);
        Assert.Empty(stats.BossesEscaped);
    }

    [Fact]
    public async Task The_community_counts_distinct_species_trainers_fights_and_boss_catches()
    {
        await AddTrainerAsync("1", "Ash", 25, 16, 16);
        await AddTrainerAsync("2", "Misty", 25, 129);
        await AddBossAsync(59, true, ("1", true), ("2", false));
        await AddBossAsync(59, false, ("1", false));
        await AddPvpAsync(("1", true), ("2", false));

        var stats = await StatisticsAsync();

        Assert.Equal(new CommunityTotals(3, 151, 2, 3, 1), stats.Community);
    }

    [Fact]
    public async Task Boss_participations_rank_with_shared_places_and_names_on_a_tie()
    {
        await AddTrainerAsync("1", "Ben");
        await AddTrainerAsync("2", "anna");
        await AddTrainerAsync("3", "Clara");
        await AddTrainerAsync("4", "Dora");
        await AddBossAsync(59, true, ("1", true), ("2", true), ("3", true));
        await AddBossAsync(59, true, ("1", true), ("2", true));

        var stats = await StatisticsAsync();

        Assert.Equal(
            [(1, "anna", 2), (1, "Ben", 2), (3, "Clara", 1)],
            stats.BossParticipations.Select(x => (x.Rank, x.Trainer.DisplayName, x.Count)));
    }

    [Fact]
    public async Task Pvp_fights_count_only_pvp_with_the_wins()
    {
        await AddTrainerAsync("1", "Ash");
        await AddTrainerAsync("2", "Misty");
        await AddPvpAsync(("1", true), ("2", false));
        await AddPvpAsync(("1", false), ("2", true));
        await AddPvpAsync(("1", true), ("2", false));
        await AddBattleAsync(BattleKind.Npc, null, BattleOutcome.ChallengerWon, ("1", true));
        await AddBossAsync(59, true, ("2", true));

        var stats = await StatisticsAsync();

        Assert.Equal([new PvpRecord(1, Ref("1", "Ash"), 3, 2), new PvpRecord(1, Ref("2", "Misty"), 3, 1)], stats.PvpFights);
    }

    [Fact]
    public async Task The_bad_luck_streak_is_the_longest_run_without_a_catch_and_marks_a_running_one()
    {
        await AddTrainerAsync("1", "Ash");
        await AddTrainerAsync("2", "Misty");
        await AddTrainerAsync("3", "Rocko");

        // Ash: miss, miss, miss, catch, miss — longest 3, over. Misty: catch, miss, miss — 2, still running.
        // Rocko catches every time and is not listed.
        await AddBossAsync(59, false, ("1", false), ("2", true), ("3", true));
        await AddBossAsync(59, false, ("1", false), ("2", false), ("3", true));
        await AddBossAsync(59, false, ("1", false), ("2", false));
        await AddBossAsync(59, true, ("1", true), ("3", true));
        await AddBossAsync(59, false, ("1", false));

        var stats = await StatisticsAsync();

        Assert.Equal([new BadLuckStreak(1, Ref("1", "Ash"), 3, false), new BadLuckStreak(2, Ref("2", "Misty"), 2, true)], stats.BadLuck);
    }

    [Fact]
    public async Task Single_owned_species_list_ten_rarest_first_and_count_all()
    {
        // Ash owns 12 species alone, Misty shares Pikachu with him.
        await AddTrainerAsync("1", "Ash", 25, 10, 16, 129, 11, 13, 14, 17, 19, 20, 150, 1, 144);
        await AddTrainerAsync("2", "Misty", 25);

        var stats = await StatisticsAsync();

        Assert.Equal(12, stats.SingleOwnedCount);
        Assert.Equal([144, 150, 1, 10, 11, 13, 14, 16, 17, 19], stats.SingleOwned.Select(s => s.Species.Id));
        Assert.All(stats.SingleOwned, s => Assert.Equal("Ash", s.Owner.DisplayName));
    }

    [Fact]
    public async Task Rivals_are_pairs_by_pvp_fights_with_the_leader_first()
    {
        await AddTrainerAsync("1", "Ash");
        await AddTrainerAsync("2", "Misty");
        await AddTrainerAsync("3", "Rocko");
        await AddPvpAsync(("1", false), ("2", true));
        await AddPvpAsync(("2", true), ("1", false));
        await AddPvpAsync(("1", true), ("2", false));
        await AddPvpAsync(("3", true), ("1", false));

        var stats = await StatisticsAsync();

        Assert.Equal(
            [new Rivalry(1, Ref("2", "Misty"), Ref("1", "Ash"), 3, 2, 1), new Rivalry(2, Ref("3", "Rocko"), Ref("1", "Ash"), 1, 1, 0)],
            stats.Rivals);
    }

    [Fact]
    public async Task A_deleted_trainer_drops_out_of_every_list()
    {
        await AddTrainerAsync("1", "Ash", 150);
        await AddTrainerAsync("2", "Misty");
        await AddPvpAsync(("1", true), ("2", false));
        await AddBossAsync(59, false, ("1", false), ("2", false));

        await _game.Trainers.DeleteAsync("1", _ct);
        var stats = await StatisticsAsync();

        Assert.Equal(["Misty"], stats.BossParticipations.Select(x => x.Trainer.DisplayName));
        Assert.Equal(["Misty"], stats.PvpFights.Select(x => x.Trainer.DisplayName));
        Assert.Equal(["Misty"], stats.BadLuck.Select(x => x.Trainer.DisplayName));
        Assert.Empty(stats.Rivals);
        Assert.Empty(stats.SingleOwned);
        Assert.Equal(1, stats.Community.Trainers);
    }

    [Fact]
    public async Task The_boss_record_counts_appearances_and_escapes_per_species()
    {
        await AddTrainerAsync("1", "Ash");
        await AddBossAsync(59, true, ("1", true));
        await AddBossAsync(59, false, ("1", false));
        await AddBossAsync(143, false, ("1", false));
        await AddBossAsync(143, false, ("1", false));
        await AddBossAsync(16, true, ("1", true));

        var stats = await StatisticsAsync();

        Assert.Equal([(1, "Arkani", 2), (1, "Relaxo", 2), (3, "Taubsi", 1)], stats.BossesSeen.Select(b => (b.Rank, b.Species.Name, b.Count)));
        Assert.Equal([(1, "Relaxo", 2), (2, "Arkani", 1)], stats.BossesEscaped.Select(b => (b.Rank, b.Species.Name, b.Count)));
    }

    [Fact]
    public async Task A_list_holds_ten_rows_and_carries_the_chat_colour()
    {
        for (var i = 1; i <= 12; i++)
        {
            await AddTrainerAsync(i.ToString(CultureInfo.InvariantCulture), $"T{i:D2}");
        }

        await using (var db = _game.OpenLive())
        {
            (await db.Trainers.FindAsync(["1"], _ct))!.ChatColor = "#1E90FF";
            await db.SaveChangesAsync(_ct);
        }

        await AddBossAsync(59, true, [.. Enumerable.Range(1, 12).Select(i => (i.ToString(CultureInfo.InvariantCulture), true))]);

        var stats = await StatisticsAsync();

        Assert.Equal(SiteGenerator.StatisticsSize, stats.BossParticipations.Count);
        Assert.All(stats.BossParticipations, x => Assert.Equal(1, x.Rank));
        Assert.Equal("#1E90FF", stats.BossParticipations[0].Trainer.Color);
    }

    [Fact]
    public async Task The_statistics_file_renders_with_a_short_cache()
    {
        await AddTrainerAsync("1", "Ash", 25);
        await using var db = _game.OpenLive();

        var file = await _generator.RenderAsync(db, SitePaths.Statistics, Start, _ct);

        Assert.Equal(SiteGenerator.MutableCache, file!.CacheControl);
        var stats = JsonSerializer.Deserialize(file.Content, SiteJson.Default.Statistics)!;
        Assert.Equal("Ash", stats.SingleOwned.Single().Owner.DisplayName);
        Assert.Equal(Start, stats.UpdatedAt);
    }

    private static TrainerRef Ref(string id, string name) => new(id, name);

    private async Task<Statistics> StatisticsAsync()
    {
        await using var db = _game.OpenLive();
        return await _generator.StatisticsAsync(db, Start, _ct);
    }

    private async Task AddTrainerAsync(string id, string name, params int[] species)
    {
        await using var db = _game.OpenLive();
        db.Trainers.Add(new Trainer
        {
            TwitchUserId = id,
            DisplayName = name,
            Login = name.ToLowerInvariant(),
            Specimens = [.. species.Select(s => new Specimen { TrainerId = id, SpeciesId = s, CaughtAt = Start, AcquiredAt = Start })],
        });
        await db.SaveChangesAsync(_ct);
    }

    private Task AddBossAsync(int bossSpecies, bool defeated, params (string Trainer, bool Won)[] participants) =>
        AddBattleAsync(BattleKind.Boss, bossSpecies, defeated ? BattleOutcome.BossDefeated : BattleOutcome.BossEscaped, participants);

    private Task AddPvpAsync(params (string Trainer, bool Won)[] participants) =>
        AddBattleAsync(BattleKind.Pvp, null, participants[0].Won ? BattleOutcome.ChallengerWon : BattleOutcome.OpponentWon, participants);

    /// <summary>A fight one minute after the previous one; its log is empty — the statistics never read it.</summary>
    private async Task AddBattleAsync(BattleKind kind, int? bossSpecies, BattleOutcome outcome, params (string Trainer, bool Won)[] participants)
    {
        await using var db = _game.OpenLive();
        db.Battles.Add(new Battle
        {
            Kind = kind,
            FoughtAt = Start.AddMinutes(++_minute),
            BossSpeciesId = bossSpecies,
            Outcome = outcome,
            LogJson = new BattleLog([], []).ToJson(),
            Participants = [.. participants.Select(p => new BattleParticipant { TrainerId = p.Trainer, Won = p.Won })],
        });
        await db.SaveChangesAsync(_ct);
    }

    public void Dispose() => _game.Dispose();
}
