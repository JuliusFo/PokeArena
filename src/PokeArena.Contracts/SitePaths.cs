using System.Globalization;

namespace PokeArena.Contracts;

/// <summary>Object keys of the website's data files, relative to the data base URL (spec §9).</summary>
public static class SitePaths
{
    public const string Meta = "meta.json";

    public const string Leaderboard = "rangliste.json";

    public const string BattleIndex = "kaempfe.json";

    public const string Statistics = "statistik.json";

    /// <summary>Folder of the trainer pages.</summary>
    public const string TrainerPrefix = "trainer/";

    /// <summary>Folder of the fight pages.</summary>
    public const string BattlePrefix = "kampf/";

    /// <summary>Folder of the trainers' Twitch profile pictures, mirrored by the bot.</summary>
    public const string ProfileImagePrefix = "profilbild/";

    public const string Png = "image/png";

    public const string Jpeg = "image/jpeg";

    private const string Extension = ".json";
    private const string PngExtension = ".png";
    private const string JpegExtension = ".jpg";

    /// <summary>Keyed by Twitch user id, never by name (spec §1).</summary>
    public static string Trainer(string twitchUserId) => TrainerPrefix + twitchUserId + Extension;

    public static string Battle(long battleId) => BattlePrefix + battleId.ToString(CultureInfo.InvariantCulture) + Extension;

    /// <summary>
    /// The profile picture of a trainer in <paramref name="contentType"/> (<see cref="Png"/> or <see cref="Jpeg"/>).
    /// The extension follows the format, so a new picture in the other format gets a new path.
    /// </summary>
    /// <exception cref="ArgumentException">Neither PNG nor JPEG.</exception>
    public static string ProfileImage(string twitchUserId, string contentType) => contentType switch
    {
        Png => ProfileImagePrefix + twitchUserId + PngExtension,
        Jpeg => ProfileImagePrefix + twitchUserId + JpegExtension,
        _ => throw new ArgumentException($"Not a profile picture format: {contentType}", nameof(contentType)),
    };

    /// <summary>
    /// Twitch user id (digits only) and format of a <see cref="ProfileImage"/> path, or null for any other path.
    /// </summary>
    public static (string TrainerId, string ContentType)? ProfileImageOf(string path)
    {
        if (!path.StartsWith(ProfileImagePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var (contentType, extension) = path.EndsWith(PngExtension, StringComparison.Ordinal) ? (Png, PngExtension)
            : path.EndsWith(JpegExtension, StringComparison.Ordinal) ? (Jpeg, JpegExtension)
            : (null, "");
        var id = contentType is null ? "" : path[ProfileImagePrefix.Length..^extension.Length];
        return id.Length > 0 && id.All(char.IsAsciiDigit) ? (id, contentType!) : null;
    }

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
