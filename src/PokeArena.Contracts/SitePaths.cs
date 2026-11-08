using System.Globalization;

namespace PokeArena.Contracts;

/// <summary>Object keys of the website's data files, relative to the data base URL (spec §9).</summary>
public static class SitePaths
{
    public const string Meta = "meta.json";

    public const string Leaderboard = "rangliste.json";

    public const string BattleIndex = "kaempfe.json";

    /// <summary>Folder of the trainer pages.</summary>
    public const string TrainerPrefix = "trainer/";

    /// <summary>Folder of the fight pages.</summary>
    public const string BattlePrefix = "kampf/";
    private const string Extension = ".json";

    /// <summary>Keyed by Twitch user id, never by name (spec §1).</summary>
    public static string Trainer(string twitchUserId) => TrainerPrefix + twitchUserId + Extension;

    public static string Battle(long battleId) => BattlePrefix + battleId.ToString(CultureInfo.InvariantCulture) + Extension;

    /// <summary>The Twitch user id of a <see cref="Trainer"/> path, or null for any other path.</summary>
    public static string? TrainerIdOf(string path) =>
        path.StartsWith(TrainerPrefix, StringComparison.Ordinal) && path.EndsWith(Extension, StringComparison.Ordinal)
            ? path[TrainerPrefix.Length..^Extension.Length]
            : null;

    /// <summary>The battle id of a <see cref="Battle"/> path, or null for any other path.</summary>
    public static long? BattleIdOf(string path) =>
        path.StartsWith(BattlePrefix, StringComparison.Ordinal) && path.EndsWith(Extension, StringComparison.Ordinal)
        && long.TryParse(path[BattlePrefix.Length..^Extension.Length], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
}
