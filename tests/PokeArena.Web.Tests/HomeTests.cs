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

namespace PokeArena.Web.Tests;

/// <summary>The leaderboard (start page): pictures, colours, trophies, polling and load errors.</summary>
public class HomeTests : WebTestContext
{
    [Fact]
    public void The_leaderboard_lists_trainers_with_links_to_their_pages()
    {
        Put(SitePaths.Leaderboard, new Leaderboard(Now, 2, [new(1, "11", "Anna", 3, 4), new(2, "12", "Ben", 1, 1)]), SiteJson.Default.Leaderboard);

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tbody tr").Count));
        Assert.Equal("trainer/11", cut.Find("tbody a").GetAttribute("href"));
        Assert.Contains("2 Trainer · Stand 18:30 Uhr", cut.Markup);
    }

    [Fact]
    public void The_leaderboard_shows_small_pictures_and_names_in_their_chat_colour()
    {
        Put(
            SitePaths.Leaderboard,
            new Leaderboard(Now, 3, [
                new(1, "11", "Anna", 3, 4, "#1E90FF", "profilbild/11.png"),
                new(2, "12", "Ben", 1, 1, "red; background: url(x)", "https://evil.example/profilbild/12.png"),
                new(3, "13", "Cleo", 1, 1)]),
            SiteJson.Default.Leaderboard);

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("tbody tr").Count));
        Assert.Equal(
            ["https://daten.example/profilbild/11.png", null, null],
            cut.FindAll("tbody .trainer-link").Select(a => a.QuerySelector("img")?.GetAttribute("src")));
        // Every row has the circle; a picture sits inside it, so a failed one leaves the circle (review
        // feat/leaderboard-profile-colours).
        Assert.Equal(3, cut.FindAll("tbody .trainer-link > span.profile-image").Count);
        Assert.Single(cut.FindAll("tbody span.profile-image > img"));
        Assert.Equal("lazy", cut.Find("tbody img").GetAttribute("loading"));
        Assert.Equal(
            [("Anna", "trainer-link tinted", Tint.Style("#1E90FF")), ("Ben", "trainer-link", null), ("Cleo", "trainer-link", null)],
            cut.FindAll("tbody .trainer-link").Select(a => (a.TextContent.Trim(), a.GetAttribute("class"), a.GetAttribute("style"))));
    }

    [Fact]
    public void The_leaderboard_puts_a_trophy_before_the_first_three_ranks()
    {
        // Leaderboard ranks are unique (SiteGenerator breaks every tie), so gold, silver, bronze, then none.
        Put(
            SitePaths.Leaderboard,
            new Leaderboard(Now, 5, [
                new(1, "11", "Anna", 5, 5), new(2, "12", "Ben", 3, 3), new(3, "13", "Cleo", 3, 2),
                new(4, "14", "Dora", 2, 2), new(5, "15", "Emil", 1, 1)]),
            SiteJson.Default.Leaderboard);

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll("tbody tr").Count));
        Assert.Equal(
            ["trophy rank-1", "trophy rank-2", "trophy rank-3", null, null],
            cut.FindAll("tbody tr td:first-child").Select(td => td.QuerySelector("svg")?.GetAttribute("class")));
        Assert.All(cut.FindAll("svg.trophy"), svg => Assert.Equal("true", svg.GetAttribute("aria-hidden")));
        Assert.Equal(["1", "2", "3", "4", "5"], cut.FindAll("tbody tr td:first-child").Select(td => td.TextContent.Trim()));
    }

    [Fact]
    public void The_leaderboard_polls_and_explains_an_empty_game()
    {
        var cut = Render<Home>();
        cut.WaitForAssertion(() => Assert.Contains("!registrieren", cut.Markup));

        Put(SitePaths.Leaderboard, new Leaderboard(Now, 1, [new(1, "11", "Anna", 1, 1)]), SiteJson.Default.Leaderboard);
        _time.Advance(PollingPage.Interval);

        cut.WaitForAssertion(() => Assert.Contains("Anna", cut.Markup));
    }

    [Fact]
    public void An_unreachable_data_host_shows_a_hint_and_keeps_trying()
    {
        _host.Down = true;

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains("konnten gerade nicht geladen werden", cut.Markup));
        _host.Down = false;
        Put(SitePaths.Leaderboard, new Leaderboard(Now, 1, [new(1, "11", "Anna", 1, 1)]), SiteJson.Default.Leaderboard);
        _time.Advance(PollingPage.Interval);
        cut.WaitForAssertion(() => Assert.DoesNotContain("konnten gerade nicht geladen werden", cut.Markup));
    }

    [Fact]
    public void A_timeout_counts_as_a_failed_load_and_polling_goes_on()
    {
        _host.TimesOut = true;

        var cut = Render<Home>();

        cut.WaitForAssertion(() => Assert.Contains("konnten gerade nicht geladen werden", cut.Markup));
        Assert.DoesNotContain("Noch niemand dabei", cut.Markup);
        _host.TimesOut = false;
        Put(SitePaths.Leaderboard, new Leaderboard(Now.AddDays(-2), 1, [new(1, "11", "Anna", 1, 1)]), SiteJson.Default.Leaderboard);
        _time.Advance(PollingPage.Interval);
        cut.WaitForAssertion(() => Assert.Contains("Anna", cut.Markup));
        Assert.Contains("Stand 30.10.2026, 18:30 Uhr", cut.Markup);
    }
}
