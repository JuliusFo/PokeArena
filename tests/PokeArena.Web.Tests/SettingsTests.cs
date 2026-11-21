using System.Text.RegularExpressions;
using Microsoft.JSInterop;
using PokeArena.Web.Pages;
using PokeArena.Web.Services;

namespace PokeArena.Web.Tests;

/// <summary>The „Einstellungen“ page: the look and the fight log's view, kept in the viewer's browser.</summary>
public class SettingsTests : WebTestContext
{
    public SettingsTests()
    {
        // The theme script in index.html: nothing picked. Only this page talks to it — every other test stays strict.
        JSInterop.Setup<string?>(ViewerSettings.ThemeGet).SetResult(ViewerSettings.ThemeAuto);
        JSInterop.SetupVoid(ViewerSettings.ThemeSet, _ => true).SetVoidResult();
    }

    [Fact]
    public void Without_a_pick_the_look_follows_the_device_and_the_log_shows_bars()
    {
        var cut = Render<Settings>();

        Assert.Equal([("Automatisch", "true"), ("Hell", "false"), ("Dunkel", "false")], Pressed(cut, "aussehen"));
        Assert.Equal([("Balken", "true"), ("Text", "false")], Pressed(cut, "kampfverlauf"));
    }

    [Fact]
    public void Earlier_picks_are_shown()
    {
        JSInterop.Setup<string?>(ViewerSettings.ThemeGet).SetResult(ViewerSettings.ThemeDark);
        JSInterop.Setup<string?>("localStorage.getItem", ViewerSettings.LogViewKey).SetResult("text");

        var cut = Render<Settings>();

        Assert.Equal([("Automatisch", "false"), ("Hell", "false"), ("Dunkel", "true")], Pressed(cut, "aussehen"));
        Assert.Equal([("Balken", "false"), ("Text", "true")], Pressed(cut, "kampfverlauf"));
    }

    [Fact]
    public void An_unknown_stored_look_counts_as_automatic()
    {
        JSInterop.Setup<string?>(ViewerSettings.ThemeGet).SetResult("lila");

        var cut = Render<Settings>();

        Assert.Equal("true", Button(cut, "aussehen", "Automatisch").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Picking_a_look_hands_it_to_the_theme_script()
    {
        var cut = Render<Settings>();

        Button(cut, "aussehen", "Dunkel").Click();

        Assert.Equal([ViewerSettings.ThemeDark], JSInterop.VerifyInvoke(ViewerSettings.ThemeSet).Arguments);
        Assert.Equal([("Automatisch", "false"), ("Hell", "false"), ("Dunkel", "true")], Pressed(cut, "aussehen"));

        Button(cut, "aussehen", "Automatisch").Click();

        Assert.Equal([ViewerSettings.ThemeAuto], JSInterop.VerifyInvoke(ViewerSettings.ThemeSet, calledTimes: 2)[1].Arguments);
    }

    [Fact]
    public void Picking_the_log_view_stores_it_under_the_fight_pages_key()
    {
        var cut = Render<Settings>();

        Button(cut, "kampfverlauf", "Text").Click();

        Assert.Equal([ViewerSettings.LogViewKey, "text"], JSInterop.VerifyInvoke("localStorage.setItem").Arguments);
        Assert.Equal([("Balken", "false"), ("Text", "true")], Pressed(cut, "kampfverlauf"));
    }

    [Fact]
    public void Without_the_theme_script_and_storage_the_switches_still_work()
    {
        // A stale cached index.html without the script, and blocked storage.
        JSInterop.Setup<string?>(ViewerSettings.ThemeGet).SetException(new JSException("pokeArenaTheme is not defined"));
        JSInterop.SetupVoid(ViewerSettings.ThemeSet, _ => true).SetException(new JSException("pokeArenaTheme is not defined"));
        JSInterop.Setup<string?>("localStorage.getItem", ViewerSettings.LogViewKey).SetException(new JSException("SecurityError"));
        JSInterop.SetupVoid("localStorage.setItem", _ => true).SetException(new JSException("SecurityError"));

        var cut = Render<Settings>();

        Assert.Equal("true", Button(cut, "aussehen", "Automatisch").GetAttribute("aria-pressed"));
        Assert.Equal("true", Button(cut, "kampfverlauf", "Balken").GetAttribute("aria-pressed"));

        Button(cut, "aussehen", "Hell").Click();
        Button(cut, "kampfverlauf", "Text").Click();

        Assert.Equal("true", Button(cut, "aussehen", "Hell").GetAttribute("aria-pressed"));
        Assert.Equal("true", Button(cut, "kampfverlauf", "Text").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void The_dark_theme_follows_the_pick_not_only_the_device()
    {
        // A dark rule behind @media (prefers-color-scheme: dark) would ignore a viewer who picked „Hell“: every dark
        // rule hangs on data-theme, which the script at the top of index.html sets before the first paint.
        var web = Path.Combine(RepositoryRoot(), "src", "PokeArena.Web", "wwwroot");
        var css = Regex.Replace(File.ReadAllText(Path.Combine(web, "css", "site.css")), @"/\*.*?\*/", "", RegexOptions.Singleline);
        var html = File.ReadAllText(Path.Combine(web, "index.html"));

        Assert.DoesNotMatch(new Regex(@"@media[^{]*prefers-color-scheme"), css);
        Assert.Contains(":root[data-theme=\"dark\"] {", css, StringComparison.Ordinal);
        var themeScript = html.IndexOf("window.pokeArenaTheme", StringComparison.Ordinal);
        Assert.True(themeScript > 0 && themeScript < html.IndexOf("</head>", StringComparison.Ordinal), "the theme script must run in <head>, before the first paint");
    }

    [Fact]
    public void The_theme_script_speaks_the_pages_values()
    {
        // The bUnit tests fake both JS calls: only this keeps the script and ViewerSettings on the same words — a value
        // the script does not know turns into „auto“ without a sound (review claude/settings-page-design-00654c).
        var html = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "PokeArena.Web", "wwwroot", "index.html"));

        foreach (var value in new[] { ViewerSettings.ThemeAuto, ViewerSettings.ThemeLight, ViewerSettings.ThemeDark })
        {
            Assert.Contains($"'{value}'", html, StringComparison.Ordinal);
        }

        var (api, get) = (ViewerSettings.ThemeGet.Split('.')[0], ViewerSettings.ThemeGet.Split('.')[1]);
        Assert.Equal(ViewerSettings.ThemeSet, $"{api}.set");
        Assert.Contains($"window.{api} = {{", html, StringComparison.Ordinal);
        Assert.Contains($"{get}: function", html, StringComparison.Ordinal);
        Assert.Contains("set: function", html, StringComparison.Ordinal);

        // A pick in another tab applies here too.
        Assert.Contains("addEventListener('storage'", html, StringComparison.Ordinal);
    }

    private static List<(string, string?)> Pressed(IRenderedComponent<Settings> cut, string group) =>
        cut.FindAll($"[aria-labelledby='{group}'] button").Select(b => (b.TextContent, b.GetAttribute("aria-pressed"))).ToList();

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<Settings> cut, string group, string label) =>
        cut.FindAll($"[aria-labelledby='{group}'] button").Single(b => b.TextContent == label);

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PokeArena.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("PokeArena.slnx not found above the test output");
    }
}
