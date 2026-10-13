using System.Text.Json.Nodes;
using PokeArena.Core.Data;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests.Data;

public class SpeciesCatalogTests
{
    private static readonly string RealJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "pokemon.json"));

    [Fact]
    public void Real_base_data_has_151_species_and_the_five_starters_in_order()
    {
        var catalog = GameFixture.Species;

        Assert.Equal(SpeciesCatalog.ExpectedCount, catalog.All.Count);
        Assert.Equal(["Bisasam", "Schiggy", "Glumanda", "Pikachu", "Evoli"], catalog.Starters.Select(s => s.Name));
        Assert.Equal(25, catalog.Get(25).Id);
    }

    [Theory]
    [InlineData("Pikachu", 25)]
    [InlineData("  PIKACHU ", 25)]
    [InlineData("glumanda", 4)]
    [InlineData("nidoran-w", 29)]
    public void Find_matches_name_or_slug_ignoring_case(string input, int expectedId)
    {
        Assert.Equal(expectedId, GameFixture.Species.Find(input)?.Id);
    }

    [Fact]
    public void Find_starter_accepts_only_the_five_starters()
    {
        Assert.Equal(133, GameFixture.Species.FindStarter("evoli")?.Id);
        Assert.Null(GameFixture.Species.FindStarter("Mewtu"));
        Assert.Null(GameFixture.Species.FindStarter(""));
        Assert.Null(GameFixture.Species.Find("Agumon"));
    }

    [Fact]
    public void Missing_species_fails_validation()
    {
        var json = Modify(pokemon => pokemon.RemoveAt(150));

        var ex = Assert.Throws<InvalidDataException>(() => SpeciesCatalog.Parse(json));

        Assert.Contains("expected 151 species, found 150", ex.Message);
    }

    [Fact]
    public void Duplicate_slug_and_missing_starter_fail_validation()
    {
        var json = Modify(pokemon => pokemon[24]!["slug"] = "raichu");

        var ex = Assert.Throws<InvalidDataException>(() => SpeciesCatalog.Parse(json));

        Assert.Contains("duplicate slug 'raichu'", ex.Message);
        Assert.Contains("starter 'pikachu' missing", ex.Message);
    }

    [Fact]
    public void Malformed_or_missing_file_is_reported_as_invalid_data()
    {
        Assert.Throws<InvalidDataException>(() => SpeciesCatalog.Parse("{ nope"));
        Assert.Throws<InvalidDataException>(() => SpeciesCatalog.Load(Path.Combine(AppContext.BaseDirectory, "missing.json")));
    }

    private static string Modify(Action<JsonArray> change)
    {
        var root = JsonNode.Parse(RealJson)!;
        change(root["pokemon"]!.AsArray());
        return root.ToJsonString();
    }
}
