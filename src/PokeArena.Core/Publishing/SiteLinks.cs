using System.Globalization;

namespace PokeArena.Core.Publishing;

/// <summary>Links to the public website for chat messages (spec §9). Without a configured site there are none.</summary>
public sealed class SiteLinks(WebsiteOptions options)
{
    public static SiteLinks None { get; } = new(new WebsiteOptions());

    /// <summary>The site's start page, or null if no site is configured.</summary>
    public string? Home => options.HasSite ? options.SiteUrl.TrimEnd('/') : null;

    /// <summary>
    /// The page of a stored fight, or null without a site or for a dry-run fight (the sandbox is never published).
    /// </summary>
    public string? Battle(long battleId, bool isSimulated) =>
        Home is { } home && !isSimulated ? $"{home}/kampf/{battleId.ToString(CultureInfo.InvariantCulture)}" : null;

    /// <summary>Appends „ Zum Kampf: &lt;link&gt;“ to a chat message when there is a link.</summary>
    public string WithBattle(string message, long battleId, bool isSimulated) =>
        Battle(battleId, isSimulated) is { } link ? $"{message} Zum Kampf: {link}" : message;
}
