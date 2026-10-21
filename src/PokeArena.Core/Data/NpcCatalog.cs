using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeArena.Core.Data;

/// <summary>An NPC from <c>data/npcs.json</c> (spec §7), unlocked by the achievement at <see cref="Threshold"/>.</summary>
/// <param name="Threshold">Distinct species a trainer must own; also the achievement's number („AV 15“).</param>
/// <param name="Strength">Factor on the team's base values in an NPC fight.</param>
/// <param name="Tokens">Credited to the trainer for every win against this NPC (spec §7).</param>
public sealed record Npc(string Id, string Name, int Threshold, double Strength, int Tokens, IReadOnlyList<Species> Team);

/// <summary>
/// The 17 NPCs and with them the achievement thresholds (spec §1): every threshold unlocks exactly one NPC.
/// Loaded once at startup; team names are checked against the species.
/// </summary>
public sealed class NpcCatalog
{
    public const int ExpectedCount = 17;

    public const int TeamSize = 6;

    private readonly Dictionary<int, Npc> _byThreshold;

    private NpcCatalog(IReadOnlyList<Npc> npcs)
    {
        All = npcs;
        _byThreshold = npcs.ToDictionary(n => n.Threshold);
    }

    /// <summary>All NPCs, by ascending threshold.</summary>
    public IReadOnlyList<Npc> All { get; }

    /// <summary>The NPC unlocked by the achievement at <paramref name="threshold"/>.</summary>
    /// <exception cref="KeyNotFoundException">No achievement has this threshold.</exception>
    public Npc ForThreshold(int threshold) => _byThreshold[threshold];

    /// <summary>Thresholds reached with <paramref name="distinctSpecies"/> distinct species, ascending.</summary>
    public IEnumerable<int> ThresholdsReached(int distinctSpecies) =>
        All.Select(n => n.Threshold).Where(t => t <= distinctSpecies);

    /// <exception cref="InvalidDataException">The file is missing, malformed or fails validation.</exception>
    public static NpcCatalog Load(string path, SpeciesCatalog species)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"Base data not found: {path}");
        }

        return Parse(File.ReadAllText(path), species);
    }

    /// <exception cref="InvalidDataException">Malformed JSON or failed validation.</exception>
    public static NpcCatalog Parse(string json, SpeciesCatalog species)
    {
        NpcFile file;
        try
        {
            file = JsonSerializer.Deserialize<NpcFile>(json) ?? throw new InvalidDataException("npcs.json is empty");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"npcs.json is malformed: {ex.Message}", ex);
        }

        var problems = new List<string>();
        if (file.Npcs.Count != ExpectedCount)
        {
            problems.Add($"expected {ExpectedCount} NPCs, found {file.Npcs.Count}");
        }

        problems.AddRange(file.Npcs.GroupBy(n => n.Id, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)
            .Select(g => $"duplicate id '{g.Key}'"));
        problems.AddRange(file.Npcs.GroupBy(n => n.Threshold).Where(g => g.Count() > 1)
            .Select(g => $"duplicate threshold {g.Key}"));

        var npcs = new List<Npc>();
        foreach (var entry in file.Npcs)
        {
            if (string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Name))
            {
                problems.Add("id and name are required");
            }

            if (entry.Threshold is < 1 or > SpeciesCatalog.ExpectedCount)
            {
                problems.Add($"{entry.Name}: threshold must be 1–{SpeciesCatalog.ExpectedCount}");
            }

            if (entry.Strength <= 0)
            {
                problems.Add($"{entry.Name}: strength must be positive");
            }

            if (entry.Tokens < 0)
            {
                problems.Add($"{entry.Name}: tokens must not be negative");
            }

            if (entry.Team.Count != TeamSize)
            {
                problems.Add($"{entry.Name}: team must have {TeamSize} species");
            }

            var team = new List<Species>();
            foreach (var slug in entry.Team)
            {
                if (species.Find(slug) is { } s)
                {
                    team.Add(s);
                }
                else
                {
                    problems.Add($"{entry.Name}: unknown species '{slug}'");
                }
            }

            npcs.Add(new Npc(entry.Id, entry.Name, entry.Threshold, entry.Strength, entry.Tokens, team));
        }

        if (problems.Count > 0)
        {
            throw new InvalidDataException("npcs.json is invalid: " + string.Join("; ", problems));
        }

        return new NpcCatalog([.. npcs.OrderBy(n => n.Threshold)]);
    }

    private sealed record NpcFile([property: JsonPropertyName("npcs")] IReadOnlyList<NpcEntry> Npcs);

    private sealed record NpcEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("schwelle")] int Threshold,
        [property: JsonPropertyName("staerke")] double Strength,
        [property: JsonPropertyName("tokens")] int Tokens,
        [property: JsonPropertyName("team")] IReadOnlyList<string> Team);
}
