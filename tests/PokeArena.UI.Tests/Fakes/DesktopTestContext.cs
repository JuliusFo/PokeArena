using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Core;
using PokeArena.Core.Chat;
using PokeArena.Core.Data;
using PokeArena.Core.Game;
using PokeArena.Core.Game.Battles;
using PokeArena.Core.Publishing;
using PokeArena.Core.Twitch;


namespace PokeArena.UI.Tests.Fakes;

/// <summary>The desktop app's services on a real in-memory database, for component tests of its cards and shell.</summary>
public abstract class DesktopTestContext : BunitContext
{
    protected readonly SqliteConnection _keeper;
    protected readonly DryRunMode _dryRun = new();
    protected readonly SpeciesCatalog _species = SpeciesCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "pokemon.json"));
    protected readonly NpcCatalog _npcs;
    protected readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 13, 18, 30, 0, TimeSpan.Zero));
    protected readonly GameDatabase _database;
    protected readonly TrainerService _trainers;
    protected readonly ChatInbox _inbox = new();
    protected readonly AppPaths _paths = new(Path.Combine(Path.GetTempPath(), "pokearena-ui-" + Guid.NewGuid().ToString("N")));
    protected readonly CancellationToken _ct = Xunit.TestContext.Current.CancellationToken;

    protected DesktopTestContext()
    {
        var connectionString = $"Data Source=ui-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _keeper = new SqliteConnection(connectionString);
        _keeper.Open();
        _npcs = NpcCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "npcs.json"), _species);
        _database = new GameDatabase(connectionString, _dryRun, NullLogger<GameDatabase>.Instance);
        _database.MigrateLiveAsync(_ct).GetAwaiter().GetResult();
        _trainers = new TrainerService(_database, _species, _time, NullLogger<TrainerService>.Instance);

        Services.AddSingleton(_dryRun);
        Services.AddSingleton(_database);
        Services.AddSingleton(_trainers);
        var battles = new BattleService(_database, _species, _npcs);
        Services.AddSingleton(battles);
        Services.AddSingleton(new AppInfo("0.2.0"));
        var chatLog = new ChatLog(_time);
        var inbox = _inbox;
        Services.AddSingleton(chatLog);
        Services.AddSingleton(inbox);
        Services.AddSingleton(new DryRunTrainers(_database, _species, _npcs, _trainers, PvpSettings.Default, new SystemSeedSource(), inbox, chatLog, _time));
        Services.AddSingleton<ITwitchConnection>(new FakeTwitchConnection { State = new(TwitchStatus.SignedOut) });
        Services.AddSingleton(new TrainerRemoval(_trainers, battles, new NullChatSender(), _time, NullLogger<TrainerRemoval>.Instance));
        Services.AddSingleton(new StreamStatus());
        var r2 = new R2Access(new WebsiteOptions(), new MemorySecretStore());
        Services.AddSingleton(r2);
        Services.AddSingleton(new OverlayOptions());
        Services.AddSingleton(new OverlayStatus());
        Services.AddSingleton(new DatabaseBackup(_paths, _database, _time, NullLogger<DatabaseBackup>.Instance));
        Services.AddSingleton(new SettingsFile(_paths));
        Services.AddSingleton(new TwitchOptions { Channel = "shiyofurball" });
        Services.AddSingleton(new WebsiteOptions());
        Services.AddSingleton(new UpdateCheck(new GitHubReleaseHint(new HttpClient(), new AppInfo("0.2.0")), new UpdateGate(new StreamStatus(), _dryRun, () => false, _time), _time, NullLogger<UpdateCheck>.Instance));
        Services.AddSingleton(new SitePublishWorker(_database, new SiteGenerator(_species, _npcs), r2, _time, NullLogger<SitePublishWorker>.Instance));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _database.Dispose();
            _keeper.Dispose();
            if (Directory.Exists(_paths.Root))
            {
                Directory.Delete(_paths.Root, recursive: true);
            }
        }

        base.Dispose(disposing);
    }

    private sealed class NullChatSender : IChatSender
    {
        public void Send(string text)
        {
        }
    }
}
