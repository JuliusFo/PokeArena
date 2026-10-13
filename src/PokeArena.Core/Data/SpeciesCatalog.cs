using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeArena.Core.Data;

/// <summary>One of the 151 Gen-1 species from <c>data/pokemon.json</c> (spec §2). Referenced by <see cref="Id"/>.</summary>
/// <param name="Rarity">1 = common, 2 = rare, 3 = legendary.</param>
public sealed record Species(
    int Id,
    string Name,
    string Slug,
    IReadOnlyList<string> Types,
    int Hp,
    int Atk,
    int Def,
    int Spez,
    int Rarity);

/// <summary>
/// The species base data, loaded once at startup and validated, so a broken file stops the app instead of
/// surfacing mid-game. Values are never copied into the database (ADR-0001).
/// </summary>
public sealed class SpeciesCatalog
{
    public const int ExpectedCount = 151;

    /// <summary>The five starters of spec §3, in the order they are offered.</summary>
    public static readonly IReadOnlyList<string> StarterSlugs = ["bisasam", "schiggy", "glumanda", "pikachu", "evoli"];

    private readonly Dictionary<int, Species> _byId;
    private readonly Dictionary<string, Species> _byName;

    private SpeciesCatalog(IReadOnlyList<Species> species)
    {
        All = species;
        _byId = species.ToDictionary(s => s.Id);
        _byName = new Dictionary<string, Species>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in species)
        {
            _byName[s.Slug] = s;
            _byName.TryAdd(s.Name, s);
        }

        Starters = [.. StarterSlugs.Select(slug => _byName[slug])];
    }

    public IReadOnlyList<Species> All { get; }

    public IReadOnlyList<Species> Starters { get; }

    /// <exception cref="KeyNotFoundException">No species has this id.</exception>
    public Species Get(int id) => _byId[id];

    /// <summary>Finds a species by German name or slug, ignoring case and surrounding spaces (spec §8).</summary>
    public Species? Find(string nameOrSlug) => _byName.GetValueOrDefault(nameOrSlug.Trim());

    /// <summary>Like <see cref="Find"/>, but only one of the five starters.</summary>
    public Species? FindStarter(string nameOrSlug) => Find(nameOrSlug) is { } s && Starters.Contains(s) ? s : null;

    /// <exception cref="InvalidDataException">The file is missing, malformed or fails validation.</exception>
    public static SpeciesCatalog Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"Base data not found: {path}");
        }

        return Parse(File.ReadAllText(path));
    }

    /// <exception cref="InvalidDataException">Malformed JSON or failed validation.</exception>
    public static SpeciesCatalog Parse(string json)
    {
        BaseDataFile file;
        try
        {
            file = JsonSerializer.Deserialize<BaseDataFile>(json) ?? throw new InvalidDataException("pokemon.json is empty");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"pokemon.json is malformed: {ex.Message}", ex);
        }

        var species = file.Pokemon
            .Select(p => new Species(p.Id, p.Name, p.Slug, p.Types, p.Hp, p.Atk, p.Def, p.Spez, p.Rarity))
            .OrderBy(s => s.Id)
            .ToList();
        Validate(species);
        return new SpeciesCatalog(species);
    }

    private static void Validate(List<Species> species)
    {
        var problems = new List<string>();
        if (species.Count != ExpectedCount)
        {
            problems.Add($"expected {ExpectedCount} species, found {species.Count}");
        }

        if (!species.Select(s => s.Id).SequenceEqual(Enumerable.Range(1, species.Count)))
        {
            problems.Add("ids must run from 1 without gaps or duplicates");
        }

        problems.AddRange(species
            .GroupBy(s => s.Slug, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"duplicate slug '{g.Key}'"));

        foreach (var s in species)
        {
            if (string.IsNullOrWhiteSpace(s.Name) || string.IsNullOrWhiteSpace(s.Slug))
            {
                problems.Add($"#{s.Id}: name and slug are required");
            }

            if (s.Types.Count is < 1 or > 2)
            {
                problems.Add($"#{s.Id}: one or two types required");
            }

            if (s.Hp <= 0 || s.Atk <= 0 || s.Def <= 0 || s.Spez <= 0)
            {
                problems.Add($"#{s.Id}: stats must be positive");
            }

            if (s.Rarity is < 1 or > 3)
            {
                problems.Add($"#{s.Id}: rarity must be 1–3");
            }
        }

        var slugs = species.Select(s => s.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        problems.AddRange(StarterSlugs.Where(slug => !slugs.Contains(slug)).Select(slug => $"starter '{slug}' missing"));

        if (problems.Count > 0)
        {
            throw new InvalidDataException("pokemon.json is invalid: " + string.Join("; ", problems));
        }
    }

    private sealed record BaseDataFile([property: JsonPropertyName("pokemon")] IReadOnlyList<BaseDataEntry> Pokemon);

    private sealed record BaseDataEntry(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("slug")] string Slug,
        [property: JsonPropertyName("typen")] IReadOnlyList<string> Types,
        [property: JsonPropertyName("hp")] int Hp,
        [property: JsonPropertyName("atk")] int Atk,
        [property: JsonPropertyName("def")] int Def,
        [property: JsonPropertyName("spez")] int Spez,
        [property: JsonPropertyName("seltenheit")] int Rarity);
}
