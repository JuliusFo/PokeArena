namespace PokeArena.Core.Publishing;

/// <summary>
/// The local OBS overlay (spec §9): a browser source served by the desktop app on this PC only. Settings from the
/// "PokeArena:Overlay" section.
/// </summary>
public sealed class OverlayOptions
{
    public const string SectionName = "PokeArena:Overlay";

    /// <summary>Loopback port; 0 switches the overlay off.</summary>
    public int Port { get; set; } = 5299;

    public bool Enabled => Port > 0;

    /// <summary>What the streamer enters in OBS as the browser source URL.</summary>
    public string Url => $"http://localhost:{Port}/overlay";
}
