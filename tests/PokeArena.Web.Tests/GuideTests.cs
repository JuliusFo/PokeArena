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

/// <summary>The „So geht's“ page.</summary>
public class GuideTests : WebTestContext
{
    [Fact]
    public void The_guide_explains_the_commands_and_lists_every_npc()
    {
        var cut = Render<Guide>();

        Assert.Contains("!registrieren", cut.Markup);
        Assert.Contains("!challenge", cut.Markup);
        // Every row against data/npcs.json: threshold and name, in order (review 1.0.3, C16).
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "npcs.json")));
        var expected = file.RootElement.GetProperty("npcs").EnumerateArray()
            .Select(n => $"{n.GetProperty("schwelle").GetInt32()}{n.GetProperty("name").GetString()}");
        Assert.Equal(expected, cut.FindAll(".guide-npcs tbody tr").Select(tr => tr.TextContent));
    }
}
