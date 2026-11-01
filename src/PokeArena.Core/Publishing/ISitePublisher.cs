namespace PokeArena.Core.Publishing;

/// <summary>Where site files go: the R2 bucket in production, a fake in tests.</summary>
public interface ISitePublisher
{
    /// <exception cref="SitePublishException">The upload failed.</exception>
    Task PutAsync(string path, SiteFile file, CancellationToken ct);

    /// <summary>Removes the file; succeeds if it does not exist.</summary>
    /// <exception cref="SitePublishException">The deletion failed.</exception>
    Task DeleteAsync(string path, CancellationToken ct);
}

/// <summary>Hands out the publisher for the current settings, or null while uploading is not set up.</summary>
public interface ISitePublisherFactory
{
    ISitePublisher? Create();
}

/// <summary>A failed upload. The message is plain German: the streamer reads it in the app.</summary>
public sealed class SitePublishException(string message, Exception? inner = null) : Exception(message, inner);
