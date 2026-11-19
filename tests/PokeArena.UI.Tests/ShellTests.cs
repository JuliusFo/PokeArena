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

namespace PokeArena.UI.Tests;

/// <summary>Navigation between the areas of the desktop app.</summary>
public class ShellTests : DesktopTestContext
{
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
}
