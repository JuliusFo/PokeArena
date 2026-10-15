using PokeArena.Core.Data;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests.Data;

/// <summary>Type chart and NPCs from the real base data files.</summary>
public class BaseDataTests
{
    private static readonly string RealTypesJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "typen.json"));
    private static readonly string RealNpcsJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "npcs.json"));

    [Theory]
    [InlineData("Wasser", "Feuer", 2.0)]
    [InlineData("Feuer", "Wasser", 0.5)]
    [InlineData("Normal", "Normal", 1.0)]
    [InlineData("Normal", "Geist", 0.25)]
    [InlineData("Eis", "Feuer", 1.0)]
    [InlineData("Gift", "Käfer", 2.0)]
    [InlineData("Käfer", "Gift", 2.0)]
    public void Type_chart_keeps_the_gen1_values_without_immunities(string attack, string defend, double expected)
    {
        Assert.Equal(expected, GameFixture.Types.Factor(attack, defend));
    }

    [Fact]
    public void Dual_types_multiply_on_defence_and_take_the_better_type_on_attack()
    {
        var types = GameFixture.Types;

        // Glurak (Feuer/Flug) defending against Gestein: 2 × 2.
        Assert.Equal(4.0, types.Effectiveness(["Gestein"], ["Feuer", "Flug"]));

        // Elektro against Geowaz (Gestein/Boden): 1 × 0.25.
        Assert.Equal(0.25, types.Effectiveness(["Elektro"], ["Gestein", "Boden"]));

        // Bisasam (Pflanze/Gift) attacking Schiggy (Wasser): Pflanze 2 beats Gift 1.
        Assert.Equal(2.0, types.Effectiveness(["Pflanze", "Gift"], ["Wasser"]));
    }

    [Fact]
    public void Type_chart_rejects_unknown_types_and_zero_factors()
    {
        var unknown = RealTypesJson.Replace("\"Drache\":  { \"Drache\": 2.0 }", "\"Drache\":  { \"Fee\": 2.0 }");
        var zero = RealTypesJson.Replace("\"Drache\":  { \"Drache\": 2.0 }", "\"Drache\":  { \"Drache\": 0 }");

        Assert.Contains("unknown defending type 'Fee'", Assert.Throws<InvalidDataException>(() => TypeChart.Parse(unknown, GameFixture.Species)).Message);
        Assert.Contains("factor must be positive", Assert.Throws<InvalidDataException>(() => TypeChart.Parse(zero, GameFixture.Species)).Message);
    }

    [Fact]
    public void Real_npcs_match_the_achievement_thresholds_of_the_spec()
    {
        var npcs = GameFixture.Npcs;

        Assert.Equal(
            [15, 25, 40, 60, 70, 80, 90, 100, 105, 110, 115, 130, 135, 140, 145, 150, 151],
            npcs.All.Select(n => n.Threshold));
        Assert.All(npcs.All, n => Assert.Equal(NpcCatalog.TeamSize, n.Team.Count));
        Assert.Equal("Eich", npcs.ForThreshold(15).Name);
        Assert.Equal(["Bisasam", "Schiggy", "Glumanda", "Raupy", "Hornliu", "Pikachu"], npcs.ForThreshold(15).Team.Select(s => s.Name));
        Assert.Equal([15, 25], npcs.ThresholdsReached(39));
    }

    [Fact]
    public void Npcs_reject_unknown_species_and_duplicate_thresholds()
    {
        var unknown = RealNpcsJson.Replace("\"hornliu\"", "\"agumon\"");
        var duplicate = RealNpcsJson.Replace("\"schwelle\": 25", "\"schwelle\": 15");

        Assert.Contains("unknown species 'agumon'", Assert.Throws<InvalidDataException>(() => NpcCatalog.Parse(unknown, GameFixture.Species)).Message);
        Assert.Contains("duplicate threshold 15", Assert.Throws<InvalidDataException>(() => NpcCatalog.Parse(duplicate, GameFixture.Species)).Message);
    }
}
