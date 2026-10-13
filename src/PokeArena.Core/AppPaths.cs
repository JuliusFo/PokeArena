namespace PokeArena.Core;

/// <summary>
/// Per-user locations of the app's data (logs, database; later backups).
/// Local, not roaming: the SQLite database must never be synced by a roaming profile.
/// </summary>
public sealed class AppPaths
{
    public const string AppFolderName = "PokeArena";

    public AppPaths(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
    }

    /// <summary>%LOCALAPPDATA%\PokeArena on Windows.</summary>
    public static AppPaths Default { get; } = new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName));

    public string Root { get; }

    public string LogsDirectory => Path.Combine(Root, "logs");

    /// <summary>The live game database (SQLite). Never inside the repository (ADR-0003).</summary>
    public string DatabasePath => Path.Combine(Root, "pokearena.db");

    /// <summary>The configured data directory, or the default when none is set.</summary>
    public static AppPaths Resolve(PokeArenaOptions options) =>
        string.IsNullOrWhiteSpace(options.DataDirectory) ? Default : new AppPaths(options.DataDirectory);
}
