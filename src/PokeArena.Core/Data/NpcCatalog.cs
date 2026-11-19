using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeArena.Core.Data;

/// <summary>An NPC from <c>data/npcs.json</c> (spec §7), unlocked by the achievement at <see cref="Threshold"/>.</summary>
/// <param name="Threshold">Distinct species a trainer must own; also the achievement's number („AV 15“).</param>
/// <param name="Strength">Factor on the team's base values in an NPC fight.</param>
/// <param name="Tokens">Credited to the trainer for the first win against this NPC (spec §7).</param>
public sealed record Npc(string Id, string Name, int Threshold, double Strength, int Tokens, IReadOnlyList<Species> Team);

/// <summary>
/// The 17 NPCs and with them the achievement thresholds (spec §1): every threshold unlocks exactly one NPC.
/// Loaded once at startup; team names are checked against the species.
/// </summary>
public sealed class NpcCatalog
{
    public const int ExpectedCount = 17;

    public const int TeamSize = 6;

    /// <summary>
    /// The achievement thresholds of spec §1. Unlocks are stored by threshold only, so changing one in
    /// <c>npcs.json</c> would silently revoke achievements (review 1.8.1, A11): that needs a data migration first.
    /// </summary>
    public static readonly IReadOnlyList<int> Thresholds = [15, 25, 40, 60, 70, 80, 90, 100, 105, 110, 115, 130, 135, 140, 145, 150, 151];

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

    /// <summary>The NPC called <paramref name="name"/> (name or id, case-insensitive), or null.</summary>
    public Npc? Find(string name)
    {
        var trimmed = name.Trim();
        return All.FirstOrDefault(n =>
            string.Equals(n.Name, trimmed, StringComparison.OrdinalIgnoreCase) || string.Equals(n.Id, trimmed, StringComparison.OrdinalIgnoreCase));
    }

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

        // A missing key or a null entry is a readable problem, not a NullReferenceException (review 1.8.1, A13).
        var entries = (file.Npcs ?? []).Where(n => n is not null).ToList();
        var problems = new List<string>();
        if (entries.Count != ExpectedCount)
        {
            problems.Add($"expected {ExpectedCount} NPCs, found {entries.Count}");
        }

        problems.AddRange(entries.GroupBy(n => n.Id ?? "", StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)
            .Select(g => $"duplicate id '{g.Key}'"));
        problems.AddRange(entries.GroupBy(n => n.Threshold).Where(g => g.Count() > 1)
            .Select(g => $"duplicate threshold {g.Key}"));
        if (!entries.Select(n => n.Threshold).Order().SequenceEqual(Thresholds))
        {
            problems.Add($"thresholds must be those of spec §1 ({string.Join(", ", Thresholds)}); changing one revokes achievements");
        }

        var npcs = new List<Npc>();
        foreach (var entry in entries)
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

            var slugs = entry.Team ?? [];
            if (slugs.Count != TeamSize)
            {
                problems.Add($"{entry.Name}: team must have {TeamSize} species");
            }

            var team = new List<Species>();
            foreach (var slug in slugs)
            {
                if (slug is null)
                {
                    problems.Add($"{entry.Name}: empty team slot");
                }
                else if (species.Find(slug) is { } s)
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

    private sealed record NpcFile([property: JsonPropertyName("npcs")] IReadOnlyList<NpcEntry>? Npcs);

    private sealed record NpcEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("schwelle")] int Threshold,
        [property: JsonPropertyName("staerke")] double Strength,
        [property: JsonPropertyName("tokens")] int Tokens,
        [property: JsonPropertyName("team")] IReadOnlyList<string> Team);
}
