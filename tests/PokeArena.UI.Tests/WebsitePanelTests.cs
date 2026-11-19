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
    public void Removing_the_key_asks_first_and_then_asks_for_the_key_again()
    {
        R2.SaveCredentials("key-id", "geheim");
        var cut = Render<WebsitePanel>();

        // One click only asks (review 1.8.1, A9); „Abbrechen“ keeps the key.
        cut.FindAll("button").Single(b => b.TextContent == "Schlüssel entfernen").Click();
        Assert.Contains("Schlüssel wirklich entfernen?", cut.Find("[data-testid=confirm-removal]").TextContent);
        Assert.Equal(2, _secrets.Values.Count);
        cut.FindAll("button").Single(b => b.TextContent == "Abbrechen").Click();
        Assert.Empty(cut.FindAll("[data-testid=confirm-removal]"));
        Assert.Equal(2, _secrets.Values.Count);

        cut.FindAll("button").Single(b => b.TextContent == "Schlüssel entfernen").Click();
        cut.FindAll("button").Single(b => b.TextContent == "Ja, entfernen").Click();

        cut.WaitForAssertion(() => Assert.Contains("Schlüssel fehlt", cut.Markup));
        Assert.Empty(_secrets.Values);
    }

    [Fact]
    public void A_republish_that_fails_unexpectedly_shows_a_hint_in_the_card()
    {
        R2.SaveCredentials("key-id", "geheim");
        var species = SpeciesCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "pokemon.json"));
        var npcs = NpcCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "npcs.json"), species);
        Services.AddSingleton(new SitePublishWorker(_database, new SiteGenerator(species, npcs), new BrokenBucket(), new FakeTimeProvider(), NullLogger<SitePublishWorker>.Instance));
        var cut = Render<WebsitePanel>();

        cut.FindAll("button").Single(b => b.TextContent == "Alles neu veröffentlichen").Click();

        // Not a SitePublishException: before review 1.8.1 (A8) it reached Blazor's error UI.
        cut.WaitForAssertion(() => Assert.Contains("nicht geklappt", cut.Find("[data-testid=republish-problem]").TextContent));
        Assert.False(cut.FindAll("button").Single(b => b.TextContent == "Alles neu veröffentlichen").HasAttribute("disabled"));
    }

    /// <summary>A bucket that fails with an error the publisher does not translate.</summary>
    private sealed class BrokenBucket : ISitePublisherFactory, ISitePublisher
    {
        public event Action? Changed { add { } remove { } }

        public ISitePublisher? Create() => this;

        public Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken ct) => throw new InvalidOperationException("unexpected");

        public Task PutAsync(string path, SiteFile file, CancellationToken ct) => throw new InvalidOperationException("unexpected");

        public Task DeleteAsync(string path, CancellationToken ct) => throw new InvalidOperationException("unexpected");
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
