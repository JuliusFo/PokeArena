using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Core;
using PokeArena.Core.Data;
using PokeArena.Core.Game;
using PokeArena.Core.Game.Battles;

namespace PokeArena.UI.Tests;

/// <summary>Battle list and log view on a real in-memory database.</summary>
public class BattleListTests : BunitContext
{
    private readonly SqliteConnection _keeper;
    private readonly DryRunMode _dryRun = new();
    private readonly SpeciesCatalog _species = SpeciesCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "pokemon.json"));
    private readonly NpcCatalog _npcs;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 15, 18, 30, 0, TimeSpan.Zero));
    private readonly GameDatabase _database;
    private readonly TrainerService _trainers;
    private readonly BattleService _battles;
    private readonly CancellationToken _ct = Xunit.TestContext.Current.CancellationToken;

    public BattleListTests()
    {
        var connectionString = $"Data Source=ui-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _keeper = new SqliteConnection(connectionString);
        _keeper.Open();
        _npcs = NpcCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "npcs.json"), _species);
        _database = new GameDatabase(connectionString, _dryRun, NullLogger<GameDatabase>.Instance);
        _database.MigrateLiveAsync(_ct).GetAwaiter().GetResult();
        _trainers = new TrainerService(_database, _species, _time, NullLogger<TrainerService>.Instance);
        _battles = new BattleService(_database, _species, _npcs);

        Services.AddSingleton(_database);
        Services.AddSingleton(_battles);
    }

    [Fact]
    public void Empty_list_explains_how_a_boss_fight_starts()
    {
        var cut = Render<BattleListPanel>();

        cut.WaitForAssertion(() => Assert.Contains("Noch keine Kämpfe", cut.Markup));
        Assert.Contains("Wähle links einen Kampf aus.", cut.Markup);
    }

    [Fact]
    public async Task Stored_fight_appears_and_a_click_shows_its_course()
    {
        var cut = Render<BattleListPanel>();
        await _trainers.RegisterAsync("1", "ash", "Ash", _species.Get(25), _ct);

        await StoreBossFightAsync();
        _battles.NotifyChanged();

        cut.WaitForAssertion(() =>
        {
            var cells = cut.FindAll("tbody tr td").Select(td => td.TextContent).ToList();
            var time = _time.GetUtcNow().ToLocalTime().ToString("dd.MM. HH:mm");
            Assert.Equal([time, "Arkani (selten)", "1", "besiegt, 1 Fang"], cells);
        });

        cut.Find("tbody tr").Click();

        cut.WaitForAssertion(() =>
        {
            var lines = cut.FindAll(".battle-log li").Select(li => li.TextContent).ToList();
            Assert.Equal(
                ["Runde 1", "Pikachu (Ash, HP 35) hat Arkani (Boss, HP 0) 200 Schaden zugefügt.", "Arkani (Boss) wurde besiegt."],
                lines);
            Assert.Contains("selected", cut.Find("tbody tr").ClassName);
        });
    }

    [Theory]
    [InlineData(BattleOutcome.ChallengerWon, "Ash gegen Misty", "Ash gewinnt Karpador")]
    [InlineData(BattleOutcome.OpponentWon, "Misty gegen Ash", "Ash gewinnt Karpador")]
    public async Task Pvp_fight_shows_challenger_opponent_winner_and_loot(BattleOutcome outcome, string opponent, string result)
    {
        var cut = Render<BattleListPanel>();
        await _trainers.RegisterAsync("1", "ash", "Ash", _species.Get(25), _ct);
        await _trainers.RegisterAsync("2", "misty", "Misty", _species.Get(7), _ct);

        // Ash wins in both cases; the outcome says who issued the challenge.
        await using (var db = _database.Open())
        {
            db.Battles.Add(new Battle
            {
                Kind = BattleKind.Pvp,
                FoughtAt = _time.GetUtcNow(),
                Seed = 7,
                LootSpecimenId = 99,
                LootSpeciesId = 129,
                Outcome = outcome,
                Rounds = 6,
                LogJson = new BattleLog([], []).ToJson(),
                Participants = [new BattleParticipant { TrainerId = "1", Won = true }, new BattleParticipant { TrainerId = "2", Won = false }],
            });
            await db.SaveChangesAsync(_ct);
        }

        _battles.NotifyChanged();

        cut.WaitForAssertion(() =>
        {
            var cells = cut.FindAll("tbody tr td").Select(td => td.TextContent).ToList();
            Assert.Equal([opponent, "2", result], cells.Skip(1));
        });
    }

    [Theory]
    [InlineData(BattleOutcome.ChallengerWon, 129, 1, "Ash gewinnt Orden, Karpador, 1 Token")]
    [InlineData(BattleOutcome.ChallengerWon, null, 3, "Ash gewinnt 3 Tokens")]
    [InlineData(BattleOutcome.OpponentWon, null, null, "Eich gewinnt")]
    public async Task Npc_fight_shows_trainer_npc_and_reward(BattleOutcome outcome, int? rewardSpecies, int? tokens, string result)
    {
        var cut = Render<BattleListPanel>();
        await _trainers.RegisterAsync("1", "ash", "Ash", _species.Get(25), _ct);

        await using (var db = _database.Open())
        {
            db.Battles.Add(new Battle
            {
                Kind = BattleKind.Npc,
                FoughtAt = _time.GetUtcNow(),
                Seed = 7,
                NpcId = "eich",
                LootSpeciesId = rewardSpecies,
                Tokens = tokens,
                Outcome = outcome,
                Rounds = 6,
                LogJson = new BattleLog([], []).ToJson(),
                Participants = [new BattleParticipant { TrainerId = "1", Won = outcome == BattleOutcome.ChallengerWon }],
            });
            await db.SaveChangesAsync(_ct);
        }

        _battles.NotifyChanged();

        cut.WaitForAssertion(() =>
        {
            var cells = cut.FindAll("tbody tr td").Select(td => td.TextContent).ToList();
            Assert.Equal(["Ash gegen Eich", "1", result], cells.Skip(1));
        });
    }

    private async Task StoreBossFightAsync()
    {
        var log = new BattleLog(
            [new LogFighter(0, 25, 35, "1", "Ash"), new LogFighter(1, 59, 200, null, "Boss")],
            [
                new BattleEvent(BattleEventKind.Attack, 1, 0, 35, 1, 0, 200),
                new BattleEvent(BattleEventKind.Fainted, 1, 1),
            ]);
        await using var db = _database.Open();
        db.Battles.Add(new Battle
        {
            Kind = BattleKind.Boss,
            FoughtAt = _time.GetUtcNow(),
            Seed = 7,
            BossSpeciesId = 59,
            Outcome = BattleOutcome.BossDefeated,
            Rounds = 1,
            LogJson = log.ToJson(),
            Participants = [new BattleParticipant { TrainerId = "1", Won = true }],
        });
        await db.SaveChangesAsync(_ct);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _database.Dispose();
            _keeper.Dispose();
        }

        base.Dispose(disposing);
    }
}
