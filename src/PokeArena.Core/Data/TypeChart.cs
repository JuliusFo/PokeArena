using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeArena.Core.Data;

/// <summary>
/// The Gen-1 type chart from <c>data/typen.json</c> (spec §2): 15 types, no immunities (house rule: 0× is 0.25).
/// Loaded once at startup and checked against the species, so an unknown type stops the app.
/// </summary>
public sealed class TypeChart
{
    public const int ExpectedCount = 15;

    private readonly Dictionary<string, Dictionary<string, double>> _factors;
    private readonly double _defaultFactor;

    private TypeChart(IReadOnlyList<string> types, Dictionary<string, Dictionary<string, double>> factors, double defaultFactor)
    {
        Types = types;
        _factors = factors;
        _defaultFactor = defaultFactor;
    }

    public IReadOnlyList<string> Types { get; }

    /// <summary>Factor of one attacking type against one defending type.</summary>
    public double Factor(string attackType, string defendType) =>
        _factors.TryGetValue(attackType, out var row) && row.TryGetValue(defendType, out var factor) ? factor : _defaultFactor;

    /// <summary>
    /// Type factor of a hit (ADR-0005): the factors over the defender's types are multiplied; a dual-type attacker
    /// uses its better type.
    /// </summary>
    public double Effectiveness(IReadOnlyList<string> attackerTypes, IReadOnlyList<string> defenderTypes) =>
        attackerTypes.Max(a => defenderTypes.Aggregate(1.0, (product, d) => product * Factor(a, d)));

    /// <exception cref="InvalidDataException">The file is missing, malformed or fails validation.</exception>
    public static TypeChart Load(string path, SpeciesCatalog species)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"Base data not found: {path}");
        }

        return Parse(File.ReadAllText(path), species);
    }

    /// <exception cref="InvalidDataException">Malformed JSON or failed validation.</exception>
    public static TypeChart Parse(string json, SpeciesCatalog species)
    {
        TypeChartFile file;
        try
        {
            file = JsonSerializer.Deserialize<TypeChartFile>(json) ?? throw new InvalidDataException("typen.json is empty");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"typen.json is malformed: {ex.Message}", ex);
        }

        var problems = new List<string>();
        var types = file.Types.ToHashSet(StringComparer.Ordinal);
        if (file.Types.Count != ExpectedCount || types.Count != ExpectedCount)
        {
            problems.Add($"expected {ExpectedCount} distinct types, found {types.Count}");
        }

        if (file.DefaultFactor <= 0)
        {
            problems.Add("standardfaktor must be positive");
        }

        foreach (var (attack, row) in file.Factors)
        {
            if (!types.Contains(attack))
            {
                problems.Add($"unknown attacking type '{attack}'");
            }

            foreach (var (defend, factor) in row)
            {
                if (!types.Contains(defend))
                {
                    problems.Add($"unknown defending type '{defend}' in row '{attack}'");
                }

                // No immunities (house rule): a 0 would make stalemates possible again.
                if (factor <= 0)
                {
                    problems.Add($"{attack} → {defend}: factor must be positive");
                }
            }
        }

        problems.AddRange(species.All
            .SelectMany(s => s.Types.Where(t => !types.Contains(t)).Select(t => $"#{s.Id} {s.Name}: unknown type '{t}'")));

        if (problems.Count > 0)
        {
            throw new InvalidDataException("typen.json is invalid: " + string.Join("; ", problems));
        }

        var factors = file.Factors.ToDictionary(
            r => r.Key,
            r => new Dictionary<string, double>(r.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
        return new TypeChart(file.Types, factors, file.DefaultFactor);
    }

    private sealed record TypeChartFile(
        [property: JsonPropertyName("standardfaktor")] double DefaultFactor,
        [property: JsonPropertyName("typen")] IReadOnlyList<string> Types,
        [property: JsonPropertyName("faktoren")] Dictionary<string, Dictionary<string, double>> Factors);
}
