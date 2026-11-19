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

/// <summary>The „Neuigkeiten“ page.</summary>
public class NewsTests : WebTestContext
{
    [Fact]
    public void The_news_lists_the_newest_version_first_and_dates_every_released_one()
    {
        var cut = Render<News>();

        var headings = cut.FindAll(".news h2").Select(h => h.TextContent.Trim()).ToList();
        Assert.NotEmpty(headings);

        // Only the newest section may still lack its date — /release sets it (WP-10).
        var dated = new Regex(@"^Version (\d+\.\d+\.\d+) – \d{1,2}\. \p{L}+ \d{4}$");
        var undated = new Regex(@"^Version (\d+\.\d+\.\d+)$");
        Assert.Matches(new Regex($"{undated}|{dated}"), headings[0]);
        foreach (var heading in headings.Skip(1))
        {
            Assert.Matches(dated, heading);
        }

        var versions = headings.Select(h => Version.Parse(Regex.Match(h, @"^Version (\d+\.\d+\.\d+)").Groups[1].Value)).ToList();
        Assert.Equal(versions.OrderDescending(), versions);
        Assert.Equal(versions.Count, versions.Distinct().Count());
    }
}
