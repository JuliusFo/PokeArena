using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Core;
using PokeArena.Core.Data;
using PokeArena.Core.Publishing;

namespace PokeArena.UI.Tests;

public class WebsitePanelTests : BunitContext
{
    private readonly SqliteConnection _keeper;
    private readonly MemorySecretStore _secrets = new();
    private readonly GameDatabase _database;
    private readonly SitePublishWorker _worker;

    public WebsitePanelTests()
    {
        var connectionString = $"Data Source=web-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _keeper = new SqliteConnection(connectionString);
        _keeper.Open();
        _database = new GameDatabase(connectionString, new DryRunMode(), NullLogger<GameDatabase>.Instance);
        _database.MigrateLiveAsync(Xunit.TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        var species = SpeciesCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "pokemon.json"));
        var npcs = NpcCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "npcs.json"), species);
        R2 = new R2Access(Options, _secrets);
        _worker = new SitePublishWorker(_database, new SiteGenerator(species, npcs), R2, new FakeTimeProvider(), NullLogger<SitePublishWorker>.Instance);
        Services.AddSingleton(R2);
        Services.AddSingleton(_worker);
    }

    private WebsiteOptions Options { get; } = new()
    {
        SiteUrl = "https://arena.example.de",
        R2Endpoint = "https://account.r2.cloudflarestorage.com",
        Bucket = "pokearena",
    };

    private R2Access R2 { get; }

    [Fact]
    public void Without_a_bucket_the_streamer_is_sent_to_whoever_set_up_the_app()
    {
        Options.Bucket = "";

        var cut = Render<WebsitePanel>();

        Assert.Contains("Nicht eingerichtet", cut.Markup);
        Assert.Empty(cut.FindAll("input"));
    }

    [Fact]
    public void The_key_is_entered_once_and_stored_in_the_secret_store()
    {
        var cut = Render<WebsitePanel>();
        Assert.Contains("Schlüssel fehlt", cut.Markup);
        Assert.True(cut.Find("button[type=submit]").HasAttribute("disabled"));

        var inputs = cut.FindAll("input");
        inputs[0].Input(" key-id ");
        cut.FindAll("input")[1].Input("geheim");
        cut.Find("form").Submit();

        Assert.Equal("key-id", _secrets.Values[R2Access.AccessKeyIdSecret]);
        Assert.Equal("geheim", _secrets.Values[R2Access.SecretAccessKeySecret]);
        cut.WaitForAssertion(() => Assert.Contains("Website ist aktuell", cut.Markup));
        Assert.Contains("https://arena.example.de", cut.Markup);
        Assert.DoesNotContain("geheim", cut.Markup);
    }

    [Fact]
    public void Removing_the_key_asks_for_it_again()
    {
        R2.SaveCredentials("key-id", "geheim");
        var cut = Render<WebsitePanel>();

        cut.FindAll("button").Single(b => b.TextContent == "Schlüssel entfernen").Click();

        cut.WaitForAssertion(() => Assert.Contains("Schlüssel fehlt", cut.Markup));
        Assert.Empty(_secrets.Values);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            R2.Dispose();
            _database.Dispose();
            _keeper.Dispose();
        }

        base.Dispose(disposing);
    }
}
