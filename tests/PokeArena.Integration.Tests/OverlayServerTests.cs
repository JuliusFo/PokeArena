using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using PokeArena.Contracts;
using PokeArena.Core;
using PokeArena.Core.Data;
using PokeArena.Core.Game;
using PokeArena.Core.Publishing;
using PokeArena.Desktop.Services;

namespace PokeArena.Integration.Tests;

/// <summary>The overlay's Kestrel endpoint on a real loopback port, with an in-memory database.</summary>
public sealed class OverlayServerTests : IAsyncDisposable
{
    private readonly SqliteConnection _keeper;
    private readonly GameDatabase _database;
    private readonly SiteGenerator _generator;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public OverlayServerTests()
    {
        var connectionString = $"Data Source=overlay-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _keeper = new SqliteConnection(connectionString);
        _keeper.Open();
        _database = new GameDatabase(connectionString, new DryRunMode(), NullLogger<GameDatabase>.Instance);
        _database.MigrateLiveAsync(_ct).GetAwaiter().GetResult();
        var species = SpeciesCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "pokemon.json"));
        var npcs = NpcCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "npcs.json"), species);
        _generator = new SiteGenerator(species, npcs);
        _trainers = new TrainerService(_database, species, TimeProvider.System, NullLogger<TrainerService>.Instance);
        _battles = new BattleService(_database, species, npcs);
    }

    private readonly OverlayStatus _status = new();
    private readonly TrainerService _trainers;
    private readonly BattleService _battles;

    [Fact]
    public async Task Serves_the_page_and_the_current_state()
    {
        await using (var db = _database.Open())
        {
            db.Trainers.Add(new Trainer
            {
                TwitchUserId = "1",
                DisplayName = "Ash",
                Login = "ash",
                Specimens = [new Specimen { TrainerId = "1", SpeciesId = 25, IsStarter = true }],
            });
            await db.SaveChangesAsync(_ct);
        }

        var options = new OverlayOptions { Port = FreePort() };
        await using var server = Server(options);
        await server.StartAsync(_ct);
        using var http = new HttpClient();

        var page = await http.GetStringAsync(options.Url, _ct);
        var state = JsonSerializer.Deserialize(await http.GetStringAsync(options.Url + "/state", _ct), SiteJson.Default.OverlayState)!;

        Assert.Contains("/overlay/state", page);
        Assert.Null(state.LatestBattle);
        Assert.Equal([new LeaderboardEntry(1, "1", "Ash", 1, 1)], state.Top);

        // The state is cached until a change is reported.
        await using (var db = _database.Open())
        {
            db.Specimens.Add(new Specimen { TrainerId = "1", SpeciesId = 4 });
            await db.SaveChangesAsync(_ct);
        }

        var cached = JsonSerializer.Deserialize(await http.GetStringAsync(options.Url + "/state", _ct), SiteJson.Default.OverlayState)!;
        _trainers.NotifyChanged();
        var fresh = JsonSerializer.Deserialize(await http.GetStringAsync(options.Url + "/state", _ct), SiteJson.Default.OverlayState)!;
        Assert.Equal(1, cached.Top[0].Distinct);
        Assert.Equal(2, fresh.Top[0].Distinct);
        await server.StopAsync(_ct);
    }

    [Fact]
    public async Task A_busy_port_disables_only_the_overlay()
    {
        var blocker = new TcpListener(IPAddress.Loopback, 0);
        blocker.Start();
        try
        {
            await using var server = Server(new OverlayOptions { Port = ((IPEndPoint)blocker.LocalEndpoint).Port });

            await server.StartAsync(_ct);

            Assert.False(server.IsRunning);
            Assert.True(_status.PortInUse);
        }
        finally
        {
            blocker.Stop();
        }
    }

    private OverlayServer Server(OverlayOptions options) =>
        new(options, _status, _database, _generator, _trainers, _battles, TimeProvider.System, NullLogger<OverlayServer>.Instance);

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public ValueTask DisposeAsync()
    {
        _database.Dispose();
        _keeper.Dispose();
        return ValueTask.CompletedTask;
    }
}
