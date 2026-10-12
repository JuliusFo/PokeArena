using System.Net;

namespace PokeArena.Core.Twitch;

/// <summary>The stored login is gone or no longer accepted; the bot account has to sign in again.</summary>
public sealed class TwitchAuthRequiredException(string message) : Exception(message);

/// <summary>Twitch answered a request with an error status.</summary>
public sealed class TwitchApiException(HttpStatusCode status, string message)
    : Exception($"Twitch API error {(int)status}: {message}")
{
    public HttpStatusCode Status { get; } = status;

    /// <summary>Server-side or rate-limit errors that may pass by themselves.</summary>
    public bool IsTransient => Status == HttpStatusCode.TooManyRequests || (int)Status >= 500;
}
