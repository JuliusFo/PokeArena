// Generates data/pokemon.json from PokeAPI with Generation-I values.
// Usage (from the repository root): dotnet run tools/GeneratePokemonData.cs
//
// - Stats and types are taken as they were in Generation I (PokeAPI `past_stats` /
//   `past_types` hold values that changed later, e.g. Pikachu DEF 30, Magnetilo pure Elektro).
// - `spez` is the Gen-1 Special stat.
// - Rarity: see the rule below; it is a starting point meant to be reviewed by hand.

using System.Text;
using System.Text.Json.Nodes;

const int SpeciesCount = 151;
const string OutputPath = "data/pokemon.json";

// Rarity rule: 3 = legendary, 2 = rare (Gen-1 base stat total >= threshold, or rare in the
// original games: gift/fossil/one-of-a-kind encounters, starter lines, Pikachu/Evoli lines),
// 1 = common.
int[] legendary = [144, 145, 146, 150, 151];
const int RareTotalThreshold = 425;
int[] rareByGame =
[
    1, 2, 4, 5, 7, 8,            // starter lines (final stages qualify by stat total)
    25, 26, 133,                 // Pikachu, Raichu, Evoli
    83, 106, 107, 113, 115, 122, // Porenta, Kicklee, Nockchan, Chaneira, Kangama, Pantimos
    123, 124, 125, 126,          // Sichlor, Rossana, Elektek, Magmar
    132, 137, 138, 140,          // Ditto, Porygon, Amonitas, Kabuto
    147, 148,                    // Dratini, Dragonir
];

var typeNames = new Dictionary<string, string>
{
    ["normal"] = "Normal", ["fire"] = "Feuer", ["water"] = "Wasser", ["electric"] = "Elektro",
    ["grass"] = "Pflanze", ["ice"] = "Eis", ["fighting"] = "Kampf", ["poison"] = "Gift",
    ["ground"] = "Boden", ["flying"] = "Flug", ["psychic"] = "Psycho", ["bug"] = "Käfer",
    ["rock"] = "Gestein", ["ghost"] = "Geist", ["dragon"] = "Drache",
};

var slugOverrides = new Dictionary<int, string> { [29] = "nidoran-w", [32] = "nidoran-m" };

using var http = new HttpClient { BaseAddress = new Uri("https://pokeapi.co/api/v2/") };
http.DefaultRequestHeaders.UserAgent.ParseAdd("PokeArena-data-generator");

var lines = new List<string>();
for (var id = 1; id <= SpeciesCount; id++)
{
    var pokemon = JsonNode.Parse(await http.GetStringAsync($"pokemon/{id}"))!;
    var species = JsonNode.Parse(await http.GetStringAsync($"pokemon-species/{id}"))!;

    var name = species["names"]!.AsArray()
        .First(n => (string)n!["language"]!["name"]! == "de")!["name"]!.GetValue<string>();
    var slug = slugOverrides.GetValueOrDefault(id) ?? Slugify(name);

    var hp = Gen1Stat(pokemon, "hp");
    var atk = Gen1Stat(pokemon, "attack");
    var def = Gen1Stat(pokemon, "defense");
    var speed = Gen1Stat(pokemon, "speed");
    var special = Gen1Stat(pokemon, "special");
    var types = Gen1Types(pokemon).Select(t => typeNames[t]).ToList();

    var total = hp + atk + def + speed + special;
    var rarity = legendary.Contains(id) ? 3
        : total >= RareTotalThreshold || rareByGame.Contains(id) ? 2
        : 1;

    var typeList = string.Join(", ", types.Select(t => $"\"{t}\""));
    lines.Add($"    {{ \"id\": {id}, \"name\": \"{name}\", \"slug\": \"{slug}\", \"typen\": [{typeList}], " +
              $"\"hp\": {hp}, \"atk\": {atk}, \"def\": {def}, \"spez\": {special}, \"seltenheit\": {rarity} }}");
    Console.WriteLine($"{id,3} {name,-12} {string.Join("/", types),-16} total {total,3} rarity {rarity}");
}

var json = new StringBuilder()
    .AppendLine("{")
    .AppendLine("  \"hinweis\": [")
    .AppendLine("    \"Generiert mit tools/GeneratePokemonData.cs aus PokeAPI, Werte wie in Generation I.\",")
    .AppendLine("    \"spez = Gen-1-Wert Spezial. seltenheit: 1 = gewoehnlich, 2 = selten, 3 = legendaer.\",")
    .AppendLine("    \"bossZiele ist optional (Standard 1, 0 = alle).\"")
    .AppendLine("  ],")
    .AppendLine("  \"pokemon\": [")
    .AppendLine(string.Join(",\n", lines))
    .AppendLine("  ]")
    .AppendLine("}")
    .ToString()
    .Replace("\r\n", "\n");
await File.WriteAllTextAsync(OutputPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
Console.WriteLine($"Wrote {OutputPath}");

// Value valid in Gen I: the oldest `past_stats` entry containing the stat, else the current value.
static int Gen1Stat(JsonNode pokemon, string stat)
{
    var past = pokemon["past_stats"]!.AsArray()
        .OrderBy(e => GenerationNumber(e!["generation"]!))
        .SelectMany(e => e!["stats"]!.AsArray())
        .FirstOrDefault(s => (string)s!["stat"]!["name"]! == stat);
    if (past is not null)
        return past["base_stat"]!.GetValue<int>();

    var current = pokemon["stats"]!.AsArray().FirstOrDefault(s => (string)s!["stat"]!["name"]! == stat)
        ?? throw new InvalidOperationException($"{pokemon["name"]}: no value for '{stat}'");
    return current["base_stat"]!.GetValue<int>();
}

// Types valid in Gen I: the oldest `past_types` entry, else the current types; ordered by slot.
static IEnumerable<string> Gen1Types(JsonNode pokemon)
{
    var past = pokemon["past_types"]!.AsArray()
        .OrderBy(e => GenerationNumber(e!["generation"]!))
        .FirstOrDefault();
    var types = (past ?? pokemon)["types"]!.AsArray();
    return types.OrderBy(t => t!["slot"]!.GetValue<int>()).Select(t => (string)t!["type"]!["name"]!);
}

static int GenerationNumber(JsonNode generation) =>
    int.Parse(((string)generation["url"]!).TrimEnd('/').Split('/').Last());

static string Slugify(string name)
{
    var s = name.ToLowerInvariant()
        .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
    return new string(s.Where(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-').ToArray());
}
