namespace PokeArena.Core.Publishing;

/// <summary>Where site files go: the R2 bucket in production, a fake in tests.</summary>
public interface ISitePublisher
{
    /// <exception cref="SitePublishException">The upload failed.</exception>
    Task PutAsync(string path, SiteFile file, CancellationToken ct);

    /// <summary>Removes the file; succeeds if it does not exist.</summary>
    /// <exception cref="SitePublishException">The deletion failed.</exception>
    Task DeleteAsync(string path, CancellationToken ct);

    /// <summary>The paths of all files whose path starts with <paramref name="prefix"/>.</summary>
    /// <exception cref="SitePublishException">The listing failed.</exception>
    Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken ct);
}

/// <summary>Hands out the publisher for the current settings, or null while uploading is not set up.</summary>
public interface ISitePublisherFactory
{
    /// <summary>Raised after the access changed (e.g. a new R2 token): waiting files are worth a new try at once.</summary>
    event Action? Changed;

    ISitePublisher? Create();
}

/// <summary>A failed upload. The message is plain German: the streamer reads it in the app.</summary>
public sealed class SitePublishException(string message, Exception? inner = null) : Exception(message, inner);
