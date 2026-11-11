using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeArena.Contracts;

/// <summary>
/// Source-generated serialization for every site file: camelCase, enums as text. No reflection, so the
/// WebAssembly build can be trimmed (ADR-0009).
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, UseStringEnumConverter = true)]
[JsonSerializable(typeof(SiteMeta))]
[JsonSerializable(typeof(Leaderboard))]
[JsonSerializable(typeof(BattleIndex))]
[JsonSerializable(typeof(BattlePage))]
[JsonSerializable(typeof(TrainerPage))]
[JsonSerializable(typeof(Statistics))]
[JsonSerializable(typeof(OverlayState))]
public sealed partial class SiteJson : JsonSerializerContext;
