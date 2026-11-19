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
    public void An_unknown_trainer_is_reported()
    {
        var cut = Render<Trainer>(p => p.Add(t => t.Key, "999"));

        cut.WaitForAssertion(() => Assert.Contains("Trainer nicht gefunden", cut.Markup));
    }
}
