using System.Text.Json;
using PokeArena.Core.Publishing;
using PokeArena.Core.Twitch;

namespace PokeArena.Core;

/// <summary>What the streamer can set in the „Einstellungen“ card.</summary>
public sealed record UserSettings(string Channel, string SiteUrl, string R2Endpoint, string Bucket);

/// <summary>
/// The streamer's settings (WP-08): <c>settings.json</c> in the data directory, not next to the program, so an update
/// that replaces the program folder keeps them. The host loads it as a configuration layer in the same shape as
/// <c>appsettings.json</c>; changes apply after a restart. Nothing secret goes in here (ADR-0002).
/// </summary>
public sealed class SettingsFile(AppPaths paths)
{
    public const string FileName = "settings.json";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string Path => System.IO.Path.Combine(paths.Root, FileName);

    public async Task SaveAsync(UserSettings settings, CancellationToken ct)
    {
        Directory.CreateDirectory(paths.Root);
        var document = new Dictionary<string, object>
        {
            [PokeArenaOptions.SectionName] = new Dictionary<string, object>
            {
                [Section(TwitchOptions.SectionName)] = new Dictionary<string, string>
                {
                    [nameof(TwitchOptions.Channel)] = settings.Channel.Trim().TrimStart('#', '@'),
                },
                [Section(WebsiteOptions.SectionName)] = new Dictionary<string, string>
                {
                    [nameof(WebsiteOptions.SiteUrl)] = settings.SiteUrl.Trim().TrimEnd('/'),
                    [nameof(WebsiteOptions.R2Endpoint)] = settings.R2Endpoint.Trim().TrimEnd('/'),
                    [nameof(WebsiteOptions.Bucket)] = settings.Bucket.Trim(),
                },
            },
        };

        // Write next to it and swap, so a crash mid-write never leaves a broken file that stops the next start.
        var temp = Path + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(document, Json), ct);
        File.Move(temp, Path, overwrite: true);
    }

    /// <returns>Null if the value is fine, otherwise a plain-German reason.</returns>
    public static string? Validate(UserSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Channel) || settings.Channel.Trim().TrimStart('#', '@').Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_')))
        {
            return "Der Kanal ist der Twitch-Name aus der Adresse twitch.tv/<name> – nur Buchstaben, Ziffern und _.";
        }

        if (!IsHttpsOrEmpty(settings.SiteUrl))
        {
            return "Die Website-Adresse muss mit https:// beginnen.";
        }

        if (!IsHttpsOrEmpty(settings.R2Endpoint))
        {
            return "Der Cloudflare-Endpunkt muss mit https:// beginnen.";
        }

        return null;
    }

    /// <summary>"PokeArena:Twitch" → "Twitch".</summary>
    private static string Section(string sectionName) => sectionName[(sectionName.LastIndexOf(':') + 1)..];

    private static bool IsHttpsOrEmpty(string value) =>
        string.IsNullOrWhiteSpace(value)
        || (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);
}
