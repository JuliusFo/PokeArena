using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Contracts;
using PokeArena.Web.Layout;
using PokeArena.Web.Shared;
using PokeArena.Web.Pages;
using PokeArena.Web.Services;

namespace PokeArena.Web.Tests.Fakes;

/// <summary>The website on a fake data host and a fake clock, with a small fight to start from.</summary>
public abstract class WebTestContext : BunitContext
{
    protected static readonly DateTimeOffset Now = new(2026, 11, 1, 17, 30, 0, TimeSpan.Zero);
    protected static readonly SpeciesInfo Bisasam = new(1, "Bisasam", ["Pflanze", "Gift"]);
    protected static readonly SpeciesInfo Raupy = new(10, "Raupy", ["Käfer"]);

    protected readonly FakeDataHost _host = new();
    protected readonly FakeTimeProvider _time = new(Now);

    protected WebTestContext()
    {
        Services.AddSingleton<TimeProvider>(_time);
        Services.AddSingleton(new SiteData(new HttpClient(_host) { BaseAddress = new Uri("https://daten.example/") }));
    }

    protected static BattlePage BattlePage(long id) => new(
        id,
        BattleKind.Pvp,
        Now,
        42,
        "Ash gegen Misty",
        "Ash besiegt Misty und bekommt Raupy von Misty!",
        [
            new BattleSide("Ash", true, [new BattleFighter(Bisasam, "Ash", "11", 40)]),
            new BattleSide("Misty", false, [new BattleFighter(Raupy, "Misty", "12", 30)]),
        ],
        ["Runde 1", "Bisasam (Ash, HP 40) hat Raupy (Misty, HP 45 → 0) 50 Schaden zugefügt."]);

    protected void Put<T>(string path, T value, JsonTypeInfo<T> type) => _host.Files[path] = JsonSerializer.Serialize(value, type);
}
