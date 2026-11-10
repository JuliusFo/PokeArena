namespace PokeArena.Core;

/// <summary>
/// Where new versions come from and how they get installed (WP-11, ADR-0010). The desktop app plugs in Velopack when it
/// was installed with <c>Setup.exe</c> or <c>Portable.zip</c>; any other copy (the old ZIP, a developer build) gets
/// <see cref="GitHubReleaseHint"/>, which only finds newer versions.
/// </summary>
public interface IAppUpdater
{
    /// <summary>True if this installation can download and install a new version by itself.</summary>
    bool CanSelfUpdate { get; }

    /// <summary>The newer version (e.g. "1.6.1"), or null if this one is current. Throws if the source cannot be read.</summary>
    Task<string?> FindNewerAsync(CancellationToken ct);

    /// <summary>Downloads the version the last <see cref="FindNewerAsync"/> found. Only called if <see cref="CanSelfUpdate"/>.</summary>
    Task DownloadAsync(CancellationToken ct);

    /// <summary>
    /// Hands the downloaded version to the installer, which waits for the app to end, and closes the app the normal
    /// way (chat queue, uploads and log are flushed). The installer then starts the new version.
    /// </summary>
    void RestartIntoUpdate();
}
