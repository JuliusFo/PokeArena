using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Contracts;
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
            Assert.Equal([time, "Boss-Kampf gegen Arkani (selten)", "1", "Arkani wurde besiegt! Gefangen von: Ash."], cells);
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
