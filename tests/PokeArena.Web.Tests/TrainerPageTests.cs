using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;
using PokeArena.Contracts;
using PokeArena.Web.Layout;
using PokeArena.Web.Shared;
using PokeArena.Web.Pages;
using PokeArena.Web.Services;

namespace PokeArena.Web.Tests;

/// <summary>The trainer page.</summary>
public class TrainerPageTests : WebTestContext
{
    [Fact]
    public void A_trainer_can_be_opened_by_name()
    {
        Put(SitePaths.Leaderboard, new Leaderboard(Now, 1, [new(4, "11", "Anna", 1, 2)]), SiteJson.Default.Leaderboard);
        Put(
            SitePaths.Trainer("11"),
            new TrainerPage("11", "Anna", Now, Now, 1, 2, 3, 5, 4, [new(Bisasam, 2, true)], [new(15, "Eich", Now)], [new("Eich", Now, 9)], []),
            SiteJson.Default.TrainerPage);

        var cut = Render<Trainer>(p => p.Add(t => t.Key, "ANNA"));

        cut.WaitForAssertion(() => Assert.Contains("Platz 4", cut.Markup));
        Assert.Contains("#001", cut.Markup);
        Assert.Contains("× 2", cut.Markup);
        Assert.Contains("Orden von Eich", cut.Markup);
        Assert.Equal("kampf/9", cut.Find(".chip-badge a").GetAttribute("href"));
    }

    [Theory]
    [InlineData("profilbild/11.png", "https://daten.example/profilbild/11.png")]
    [InlineData("profilbild/11.jpg", "https://daten.example/profilbild/11.jpg")]
    [InlineData(null, null)]
    [InlineData("https://evil.example/profilbild/11.png", null)]
    [InlineData("//evil.example/profilbild/11.png", null)]
    [InlineData("profilbild/../trainer/11.png", null)]
    public void The_profile_picture_comes_only_from_the_data_host(string? path, string? expected)
    {
        Put(
            SitePaths.Trainer("11"),
            new TrainerPage("11", "Anna", Now, Now, 1, 1, 0, 0, 0, [new(Bisasam, 1, true)], [], [], [], path),
            SiteJson.Default.TrainerPage);

        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));

        cut.WaitForAssertion(() => Assert.Contains("Anna", cut.Find(".trainer-head h1").TextContent));
        Assert.Equal(expected, cut.FindAll(".trainer-head img").SingleOrDefault()?.GetAttribute("src"));
    }

    [Theory]
    [InlineData("#1E90FF", "tinted", "--tint: #1873cc")]
    [InlineData(null, null, null)]
    [InlineData("blue", null, null)]
    public void The_trainer_name_has_the_chat_colour(string? color, string? expectedClass, string? expectedStyle)
    {
        Put(
            SitePaths.Trainer("11"),
            new TrainerPage("11", "Anna", Now, Now, 1, 1, 0, 0, 0, [new(Bisasam, 1, true)], [], [], [], Color: color),
            SiteJson.Default.TrainerPage);

        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));

        cut.WaitForAssertion(() => Assert.Contains("Anna", cut.Find(".trainer-head h1").TextContent));
        var heading = cut.Find(".trainer-head h1");
        Assert.Equal((expectedClass, expectedStyle), (heading.GetAttribute("class"), heading.GetAttribute("style")));
    }

    [Fact]
    public void The_pokedex_lists_every_species_caught_seen_and_unknown()
    {
        PutPokedex(seen: [10]);

        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));

        cut.WaitForAssertion(() => Assert.Equal(151, cut.FindAll(".dex-row").Count));
        Assert.Equal(["caught", "unknown", "unknown"], cut.FindAll(".dex-row").Take(3).Select(r => r.ClassList.First(c => c != "dex-row")));
        var raupy = cut.FindAll(".dex-row")[9];
        Assert.Contains("seen", raupy.ClassList);
        Assert.Equal("gesehen", raupy.QuerySelector(".dex-seen")!.TextContent);
        Assert.Equal("????????", cut.FindAll(".dex-row")[1].QuerySelector(".dex-unknown [aria-hidden]")!.TextContent);
        Assert.Empty(cut.FindAll(".dex-row")[1].QuerySelectorAll("button"));
        Assert.Equal("× 2", cut.FindAll(".dex-row")[0].QuerySelector(".dex-count")!.TextContent);
    }

    [Fact]
    public void The_first_catch_is_selected_and_the_box_shows_its_base_values()
    {
        PutPokedex(seen: [10]);

        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));

        cut.WaitForAssertion(() => Assert.Equal("Bisasam", cut.Find(".dex-box h3").TextContent));
        Assert.Equal(
            ["HP 45", "ATK 49", "DEF 49", "SPEZ 65"],
            cut.FindAll(".dex-box .dex-values div").Select(d => d.QuerySelector("dt")!.TextContent + " " + d.QuerySelector("dd")!.TextContent));
        Assert.Contains("Starter", cut.Find(".dex-box").TextContent);

        // A seen species shows name and types, its base values only once caught.
        cut.FindAll(".dex-row")[9].QuerySelector("button")!.Click();
        Assert.Equal("Raupy", cut.Find(".dex-box h3").TextContent);
        Assert.Contains("noch nicht gefangen", cut.Find(".dex-box").TextContent);
        Assert.Empty(cut.FindAll(".dex-box .dex-values"));
        Assert.Equal("true", cut.FindAll(".dex-row")[9].QuerySelector("button")!.GetAttribute("aria-current"));
    }

    [Fact]
    public void The_switch_hides_everything_not_caught()
    {
        PutPokedex(seen: [10]);
        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));
        cut.WaitForAssertion(() => Assert.Equal(151, cut.FindAll(".dex-row").Count));

        cut.FindAll(".dex-head .view-switch button").Single(b => b.TextContent == "Nur gefangene").Click();

        Assert.Equal(["#001"], cut.FindAll(".dex-row .dex-number").Select(n => n.TextContent));
        cut.FindAll(".dex-head .view-switch button").Single(b => b.TextContent == "Alle").Click();
        Assert.Equal(151, cut.FindAll(".dex-row").Count);
    }

    [Fact]
    public void The_switch_is_remembered_in_the_browser()
    {
        PutPokedex(seen: [10]);
        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));
        cut.WaitForAssertion(() => Assert.Equal(151, cut.FindAll(".dex-row").Count));

        cut.FindAll(".dex-head .view-switch button").Single(b => b.TextContent == "Nur gefangene").Click();
        Assert.Equal([ViewerSettings.PokedexViewKey, "gefangen"], JSInterop.VerifyInvoke("localStorage.setItem").Arguments);
        cut.FindAll(".dex-head .view-switch button").Single(b => b.TextContent == "Alle").Click();
        Assert.Equal([ViewerSettings.PokedexViewKey, "alle"], JSInterop.VerifyInvoke("localStorage.setItem", calledTimes: 2)[1].Arguments);
    }

    [Fact]
    public void A_stored_pick_opens_the_pokedex_with_the_caught_ones_only()
    {
        JSInterop.Setup<string?>("localStorage.getItem", ViewerSettings.PokedexViewKey).SetResult("gefangen");
        PutPokedex(seen: [10]);

        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));

        cut.WaitForAssertion(() => Assert.Equal(["#001"], cut.FindAll(".dex-row .dex-number").Select(n => n.TextContent)));
        Assert.Equal("true", cut.FindAll(".dex-head .view-switch button").Single(b => b.TextContent == "Nur gefangene").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void While_the_browser_has_not_answered_the_pokedex_already_shows_the_trainer()
    {
        // In WASM getItem answers later: the render before it must not show 151 unknown rows and an empty box (review).
        var pending = JSInterop.Setup<string?>("localStorage.getItem", ViewerSettings.PokedexViewKey);
        PutPokedex(seen: [10]);

        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));

        cut.WaitForAssertion(() => Assert.Equal("Bisasam", cut.Find(".dex-box h3").TextContent));
        Assert.Equal(["caught", "unknown"], cut.FindAll(".dex-row").Take(2).Select(r => r.ClassList.First(c => c != "dex-row")));
        Assert.Contains("seen", cut.FindAll(".dex-row")[9].ClassList);

        pending.SetResult("gefangen");

        cut.WaitForAssertion(() => Assert.Equal(["#001"], cut.FindAll(".dex-row .dex-number").Select(n => n.TextContent)));
    }

    [Fact]
    public void A_click_before_the_browser_answers_is_not_overwritten()
    {
        var pending = JSInterop.Setup<string?>("localStorage.getItem", ViewerSettings.PokedexViewKey);
        PutPokedex(seen: [10]);
        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));
        cut.WaitForAssertion(() => Assert.Equal(151, cut.FindAll(".dex-row").Count));

        cut.FindAll(".dex-head .view-switch button").Single(b => b.TextContent == "Nur gefangene").Click();
        pending.SetResult("alle");

        cut.WaitForAssertion(() => Assert.Equal(["#001"], cut.FindAll(".dex-row .dex-number").Select(n => n.TextContent)));
    }

    [Fact]
    public void Blocked_storage_starts_with_all_and_the_switch_still_works()
    {
        JSInterop.Setup<string?>("localStorage.getItem", ViewerSettings.PokedexViewKey).SetException(new Microsoft.JSInterop.JSException("SecurityError"));
        JSInterop.SetupVoid("localStorage.setItem", _ => true).SetException(new Microsoft.JSInterop.JSException("SecurityError"));
        PutPokedex(seen: [10]);
        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));
        cut.WaitForAssertion(() => Assert.Equal(151, cut.FindAll(".dex-row").Count));

        cut.FindAll(".dex-head .view-switch button").Single(b => b.TextContent == "Nur gefangene").Click();

        Assert.Equal(["#001"], cut.FindAll(".dex-row .dex-number").Select(n => n.TextContent));
    }

    [Theory]
    [InlineData("raU", new[] { "#010" })]
    [InlineData("#1", new[] { "#001" })]
    [InlineData("10", new[] { "#010" })]
    [InlineData("Pikachu", new string[0])]
    [InlineData("2", new string[0])]
    public void The_hidden_search_finds_caught_and_seen_species_only(string query, string[] expected)
    {
        PutPokedex(seen: [10]);
        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));
        cut.WaitForAssertion(() => Assert.Equal(151, cut.FindAll(".dex-row").Count));

        cut.Find(".dex-search-open").Click();
        cut.Find(".dex-search input").Input(query);

        Assert.Equal(expected, cut.FindAll(".dex-row .dex-number").Select(n => n.TextContent));
        Assert.Equal(expected.Length == 0, cut.FindAll(".dex-empty").Count == 1);
    }

    [Fact]
    public void Escape_closes_the_search_and_shows_everything_again()
    {
        PutPokedex(seen: [10]);
        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));
        cut.WaitForAssertion(() => Assert.Equal(151, cut.FindAll(".dex-row").Count));
        cut.Find(".dex-search-open").Click();
        cut.Find(".dex-search input").Input("raupy");

        cut.Find(".dex-search input").KeyDown("Escape");

        Assert.Equal(151, cut.FindAll(".dex-row").Count);
        Assert.Empty(cut.FindAll(".dex-search input"));
        cut.Find(".dex-search-open").Click();
        Assert.Equal("", cut.Find(".dex-search input").GetAttribute("value") ?? "");
    }

    [Fact]
    public void Without_the_species_file_the_pokedex_shows_the_catches_without_base_values()
    {
        // Between the release and the bot's update arten.json is missing; „gesehen“ needs its names.
        Put(
            SitePaths.Trainer("11"),
            new TrainerPage("11", "Anna", Now, Now, 1, 2, 0, 0, 0, [new(Bisasam, 2, true)], [], [], [], Seen: [10]),
            SiteJson.Default.TrainerPage);

        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));

        cut.WaitForAssertion(() => Assert.Equal("Bisasam", cut.Find(".dex-box h3").TextContent));
        Assert.Equal(151, cut.FindAll(".dex-row").Count);
        Assert.Empty(cut.FindAll(".dex-row.seen"));
        Assert.Empty(cut.FindAll(".dex-box .dex-values"));
    }

    [Fact]
    public void A_broken_species_file_does_not_break_the_trainer_page()
    {
        Put(SitePaths.Trainer("11"), new TrainerPage("11", "Anna", Now, Now, 1, 1, 0, 0, 0, [new(Bisasam, 1, true)], [], [], []), SiteJson.Default.TrainerPage);
        _host.Files[SitePaths.Species] = "{ kaputt";

        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));

        cut.WaitForAssertion(() => Assert.Equal("Bisasam", cut.Find(".dex-box h3").TextContent));
        Assert.Empty(cut.FindAll(".dex-box .dex-values"));
    }

    [Theory]
    [InlineData("{\"species\":null}")]
    [InlineData("{\"species\":[null,{\"id\":10,\"name\":null,\"types\":[\"Käfer\"]},{\"id\":25,\"name\":\"Pikachu\",\"types\":null}]}")]
    [InlineData("{\"species\":[{\"id\":10,\"name\":\"Raupy\",\"types\":[null]},{\"id\":25,\"name\":\"Pikachu\",\"types\":[\"Elektro\",null]}]}")]
    [InlineData("{\"species\":[{\"id\":2147483647,\"name\":\"Riese\",\"types\":[]},{\"id\":0,\"name\":\"Null\",\"types\":[]},{\"id\":-5,\"name\":\"Minus\",\"types\":[]}]}")]
    public void A_species_file_with_empty_fields_or_ids_outside_gen_1_does_not_break_the_page(string json)
    {
        // Readable JSON the reader accepts although fields are null — and ids that must not set how many rows are drawn.
        Put(
            SitePaths.Trainer("11"),
            new TrainerPage("11", "Anna", Now, Now, 1, 1, 0, 0, 0, [new(Bisasam, 1, true)], [], [], [], Seen: [10, 25, 2147483647, 0, -5]),
            SiteJson.Default.TrainerPage);
        _host.Files[SitePaths.Species] = json;

        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));

        cut.WaitForAssertion(() => Assert.Equal("Bisasam", cut.Find(".dex-box h3").TextContent));
        Assert.Equal(151, cut.FindAll(".dex-row").Count);
        Assert.Empty(cut.FindAll(".dex-row.seen"));
    }

    [Fact]
    public void A_filter_that_hides_the_selected_row_hides_its_box_too()
    {
        PutPokedex(seen: [10]);
        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));
        cut.WaitForAssertion(() => Assert.Equal(151, cut.FindAll(".dex-row").Count));
        cut.FindAll(".dex-row")[9].QuerySelector("button")!.Click();

        cut.FindAll(".dex-head .view-switch button").Single(b => b.TextContent == "Nur gefangene").Click();

        Assert.Empty(cut.FindAll(".dex-box h3"));
        Assert.Contains("Klick auf ein Pokémon", cut.Find(".dex-box").TextContent);
        cut.FindAll(".dex-head .view-switch button").Single(b => b.TextContent == "Alle").Click();
        Assert.Equal("Raupy", cut.Find(".dex-box h3").TextContent);
    }

    [Fact]
    public void Closing_a_search_opened_with_the_keyboard_puts_the_focus_back_on_name()
    {
        PutPokedex(seen: [10]);
        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));
        cut.WaitForAssertion(() => Assert.Equal(151, cut.FindAll(".dex-row").Count));
        cut.Find(".dex-search-open").Click(new MouseEventArgs { Detail = 0 });

        cut.Find(".dex-search-close").Click();

        // FocusAsync goes through Blazor's own JS call: first the field, then the „Name“ button again.
        var focus = JSInterop.Invocations.Where(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Equal(2, focus.Count);
        var target = Assert.IsType<ElementReference>(focus[^1].Arguments[0]);
        Assert.Equal(cut.Find(".dex-search-open").GetAttribute("blazor:elementReference"), target.Id);
        Assert.Equal("Name – Suche öffnen", cut.Find(".dex-search-open").GetAttribute("aria-label"));
        Assert.Equal("unbekannt", cut.FindAll(".dex-row")[1].QuerySelector(".visually-hidden")!.TextContent);
    }

    [Fact]
    public void A_search_opened_with_the_mouse_closes_with_escape_without_moving_the_focus_to_name()
    {
        // A focus set by script after a key draws the focus ring on „Name“ — a stray frame for a mouse user (feedback).
        PutPokedex(seen: [10]);
        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));
        cut.WaitForAssertion(() => Assert.Equal(151, cut.FindAll(".dex-row").Count));
        cut.Find(".dex-search-open").Click(new MouseEventArgs { Detail = 1 });

        cut.Find(".dex-search input").KeyDown("Escape");

        Assert.Empty(cut.FindAll(".dex-search input"));
        var focus = JSInterop.Invocations.Where(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)).ToList();
        var target = Assert.IsType<ElementReference>(Assert.Single(focus).Arguments[0]);
        Assert.NotEqual(cut.Find(".dex-search-open").GetAttribute("blazor:elementReference"), target.Id);
    }

    [Fact]
    public void A_click_on_free_space_closes_the_search_and_shows_everything_again()
    {
        PutPokedex(seen: [10]);
        var cut = Render<Trainer>(p => p.Add(t => t.Key, "11"));
        cut.WaitForAssertion(() => Assert.Equal(151, cut.FindAll(".dex-row").Count));
        cut.Find(".dex-search-open").Click(new MouseEventArgs { Detail = 1 });
        cut.Find(".dex-search input").Input("raupy");

        // index.html's script calls back on a click outside the field that hits no control.
        var watch = Assert.Single(JSInterop.Invocations, i => i.Identifier == "pokeArenaDex.watchOutside");
        Assert.NotEmpty(Assert.IsType<ElementReference>(watch.Arguments[0]).Id);
        var pokedex = Assert.IsType<DotNetObjectReference<Pokedex>>(watch.Arguments[1]).Value;
        cut.InvokeAsync(pokedex.CloseFromOutside);

        cut.WaitForAssertion(() => Assert.Equal(151, cut.FindAll(".dex-row").Count));
        Assert.Empty(cut.FindAll(".dex-search input"));
        Assert.Single(JSInterop.Invocations, i => i.Identifier == "pokeArenaDex.unwatch");
        Assert.Single(JSInterop.Invocations, i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));
        cut.Find(".dex-search-open").Click();
        Assert.Equal("", cut.Find(".dex-search input").GetAttribute("value") ?? "");
    }

    /// <summary>Anna: two Bisasam (one the starter); arten.json with Bisasam's Gen-1 values and Raupy.</summary>
    private void PutPokedex(IReadOnlyList<int>? seen)
    {
        Put(
            SitePaths.Trainer("11"),
            new TrainerPage("11", "Anna", Now, Now, 1, 2, 0, 0, 0, [new(Bisasam, 2, true)], [], [], [], Seen: seen),
            SiteJson.Default.TrainerPage);
        Put(
            SitePaths.Species,
            new SpeciesList(
            [
                .. Enumerable.Range(1, 151).Select(id => id switch
                {
                    1 => new SpeciesData(1, "Bisasam", ["Pflanze", "Gift"], 45, 49, 49, 65),
                    10 => new SpeciesData(10, "Raupy", ["Käfer"], 45, 30, 35, 20),
                    25 => new SpeciesData(25, "Pikachu", ["Elektro"], 35, 55, 30, 50),
                    _ => new SpeciesData(id, $"Art {id}", ["Normal"], 50, 50, 50, 50),
                }),
            ]),
            SiteJson.Default.SpeciesList);
    }

    [Fact]
    public void An_unknown_trainer_is_reported()
    {
        var cut = Render<Trainer>(p => p.Add(t => t.Key, "999"));

        cut.WaitForAssertion(() => Assert.Contains("Trainer nicht gefunden", cut.Markup));
    }
}
