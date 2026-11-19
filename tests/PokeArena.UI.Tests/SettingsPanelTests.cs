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

/// <summary>The backup and settings cards.</summary>
public class SettingsPanelTests : DesktopTestContext
{
    [Fact]
    public void Backup_card_shows_that_there_is_no_backup_yet_and_where_they_go()
    {
        var cut = Render<BackupPanel>();

        Assert.Equal("noch keine", cut.Find("[data-testid=latest-backup]").TextContent);
        Assert.EndsWith("backups", cut.Find(".facts .path").TextContent);
        Assert.Equal("Jetzt sichern", cut.Find("button").TextContent);
    }

    [Fact]
    public void Settings_are_checked_then_written_to_the_data_directory()
    {
        var cut = Render<SettingsPanel>();
        Assert.Equal("shiyofurball", cut.Find("input").GetAttribute("value"));

        cut.FindAll("input")[1].Input("http://arena.example.de");
        cut.Find("form").Submit();
        Assert.Equal("Die Website-Adresse muss mit https:// beginnen.", cut.Find(".problem").TextContent);
        Assert.False(File.Exists(new SettingsFile(_paths).Path));

        cut.FindAll("input")[1].Input("https://arena.example.de/");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=settings-saved]")));
        var json = File.ReadAllText(new SettingsFile(_paths).Path);
        Assert.Contains("\"Channel\": \"shiyofurball\"", json);
        Assert.Contains("\"SiteUrl\": \"https://arena.example.de\"", json);
    }
}
