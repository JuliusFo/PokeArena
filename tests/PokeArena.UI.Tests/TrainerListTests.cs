using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Core;
using PokeArena.Core.Chat;
using PokeArena.Core.Data;
using PokeArena.Core.Game;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Twitch;

namespace PokeArena.UI.Tests;

/// <summary>Trainer list and navigation on a real in-memory database.</summary>
public class TrainerListTests : BunitContext
{
    private readonly SqliteConnection _keeper;
    private readonly DryRunMode _dryRun = new();
    private readonly SpeciesCatalog _species = SpeciesCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "pokemon.json"));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 13, 18, 30, 0, TimeSpan.Zero));
    private readonly GameDatabase _database;
    private readonly TrainerService _trainers;
    private readonly CancellationToken _ct = Xunit.TestContext.Current.CancellationToken;

    public TrainerListTests()
    {
        var connectionString = $"Data Source=ui-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _keeper = new SqliteConnection(connectionString);
        _keeper.Open();
        _database = new GameDatabase(connectionString, _dryRun, NullLogger<GameDatabase>.Instance);
        _database.MigrateLiveAsync(_ct).GetAwaiter().GetResult();
        _trainers = new TrainerService(_database, _species, _time, NullLogger<TrainerService>.Instance);

        Services.AddSingleton(_dryRun);
        Services.AddSingleton(_database);
        Services.AddSingleton(_trainers);
        Services.AddSingleton(new BattleService(_database, _species));
        Services.AddSingleton(new AppInfo("0.2.0"));
        var chatLog = new ChatLog(_time);
        var inbox = new ChatInbox();
        Services.AddSingleton(chatLog);
        Services.AddSingleton(inbox);
        Services.AddSingleton(new DryRunTrainers(_database, _species, _trainers, PvpSettings.Default, new SystemSeedSource(), inbox, chatLog, _time));
        Services.AddSingleton<ITwitchConnection>(new FakeTwitchConnection());
    }

    [Fact]
    public void Empty_list_explains_how_viewers_join()
    {
        var cut = Render<TrainerListPanel>();

        cut.WaitForAssertion(() => Assert.Contains("Noch niemand registriert", cut.Markup));
    }

    [Fact]
    public async Task New_registrations_appear_with_starter_and_count()
    {
        var cut = Render<TrainerListPanel>();

        await _trainers.RegisterAsync("1001", "ash", "Ash", _species.Get(25), _ct);

        cut.WaitForAssertion(() =>
        {
            var cells = cut.FindAll("tbody tr td").Select(td => td.TextContent).ToList();
            Assert.Equal("Ash", cells[0]);
            Assert.Equal("Pikachu", cells[1]);
            Assert.Equal("1", cells[2]);
        });
        Assert.Equal("1 Trainer", cut.Find(".count").TextContent);
    }

    [Fact]
    public async Task Search_filters_by_name()
    {
        await _trainers.RegisterAsync("1", "misty", "Misty", _species.Get(7), _ct);
        await _trainers.RegisterAsync("2", "rocko", "Rocko", _species.Get(4), _ct);
        var cut = Render<TrainerListPanel>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tbody tr").Count));

        cut.Find("input.search").Input("mis");

        Assert.Equal("Misty", cut.Find("tbody tr td").TextContent);
        cut.Find("input.search").Input("Brock");
        Assert.Contains("Niemand passt zu", cut.Markup);
    }

    [Fact]
    public async Task Dry_run_shows_only_test_trainers_and_says_so()
    {
        await _trainers.RegisterAsync("1", "misty", "Misty", _species.Get(7), _ct);
        var cut = Render<TrainerListPanel>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("tbody tr")));

        _dryRun.Enabled = true;

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("nur Testtrainer", cut.Markup);
            Assert.Contains("Noch niemand registriert", cut.Markup);
        });
    }

    [Fact]
    public void Navigation_switches_to_the_trainer_area_and_marks_the_dry_run()
    {
        var cut = Render<Shell>();
        Assert.Contains(cut.FindAll("section h2"), h => h.TextContent == "Twitch-Verbindung");

        cut.FindAll("button.tab").Single(b => b.TextContent == "Trainer").Click();
        _dryRun.Enabled = true;

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(cut.FindAll("section h2"), h => h.TextContent == "Trainer");
            Assert.Equal("Probelauf aktiv", cut.Find(".tag-dry-run").TextContent);
        });
    }

    [Fact]
    public void Navigation_switches_to_the_battle_area()
    {
        var cut = Render<Shell>();

        cut.FindAll("button.tab").Single(b => b.TextContent == "Kämpfe").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(cut.FindAll("section h2"), h => h.TextContent == "Kämpfe");
            Assert.Contains(cut.FindAll("section h2"), h => h.TextContent == "Kampfverlauf");
        });
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

    private sealed class FakeTwitchConnection : ITwitchConnection
    {
        public TwitchConnectionState State => new(TwitchStatus.SignedOut);

        public string Channel => "shiyofurball";

        public event Action? StateChanged
        {
            add { }
            remove { }
        }

        public Task BeginLoginAsync() => Task.CompletedTask;

        public void CancelLogin()
        {
        }

        public void Connect()
        {
        }

        public Task DisconnectAsync() => Task.CompletedTask;

        public Task SignOutAsync() => Task.CompletedTask;
    }
}
