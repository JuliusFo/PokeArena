namespace PokeArena.Core;

/// <summary>
/// Static configuration from the "PokeArena" section (appsettings.json, command line
/// <c>--PokeArena:DataDirectory=...</c>). Balancing values are runtime settings in the database, not here.
/// </summary>
public sealed class PokeArenaOptions
{
    public const string SectionName = "PokeArena";

    /// <summary>Redirects logs and data, e.g. for tests or a second profile. Empty: <see cref="AppPaths.Default"/>.</summary>
    public string? DataDirectory { get; set; }
}
