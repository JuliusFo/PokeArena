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

/// <summary>The layout around every page, and what the website may reference.</summary>
public class LayoutTests : WebTestContext
{
    [Fact]
    public void Data_in_an_older_format_gets_a_hint()
    {
        Put(SitePaths.Meta, new SiteMeta(SiteMeta.CurrentFormatVersion - 1, Now), SiteJson.Default.SiteMeta);

        var cut = Render<MainLayout>(p => p.Add(l => l.Body, b => b.AddContent(0, "Inhalt")));

        cut.WaitForAssertion(() => Assert.Contains("Die Daten werden gerade umgestellt", cut.Markup));
    }

    [Fact]
    public void The_website_references_only_the_contracts()
    {
        // ADR-0009: EF Core, the Twitch client and the bot's logic must never end up in the browser download.
        var references = typeof(Format).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.Contains("PokeArena.Contracts", references);
        Assert.DoesNotContain(references, r => r is "PokeArena.Core" or "PokeArena.UI" or "PokeArena.Desktop" || r.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void The_menu_links_to_the_statistics_the_guide_and_the_news()
    {
        var cut = Render<MainLayout>(p => p.Add(l => l.Body, b => b.AddContent(0, "Inhalt")));

        var links = cut.FindAll(".site-header nav a").Select(a => a.GetAttribute("href")).ToList();
        Assert.Contains("statistik", links);
        Assert.Contains("anleitung", links);
        Assert.Contains("neuigkeiten", links);
    }
}
