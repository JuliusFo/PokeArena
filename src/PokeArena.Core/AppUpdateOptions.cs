namespace PokeArena.Core;

/// <summary>Where the desktop app looks for new versions (WP-11, section <c>PokeArena:Update</c>).</summary>
public sealed class AppUpdateOptions
{
    public const string SectionName = "PokeArena:Update";

    public const string GitHubRepository = "https://github.com/JuliusFo/PokeArena";

    /// <summary>
    /// The GitHub repository whose releases carry the update packages, or a local folder with packages from
    /// <c>vpk pack</c> — only for trying an update without a release (<c>--PokeArena:Update:Source=&lt;folder&gt;</c>).
    /// </summary>
    public string Source { get; set; } = GitHubRepository;
}
