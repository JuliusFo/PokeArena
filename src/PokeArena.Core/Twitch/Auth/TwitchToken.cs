namespace PokeArena.Core.Twitch.Auth;

/// <summary>
/// Tokens of the bot account. The refresh token is single-use: every refresh returns a new one that must be
/// stored at once (ADR-0002 addendum).
/// </summary>
public sealed record TwitchToken(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

/// <summary>An open device login: the user enters <see cref="UserCode"/> at <see cref="VerificationUri"/>.</summary>
public sealed record DeviceAuthorization(
    string DeviceCode,
    string UserCode,
    Uri VerificationUri,
    DateTimeOffset ExpiresAt,
    TimeSpan PollInterval);

/// <summary>Outcome of one poll of the device login.</summary>
public abstract record DevicePollResult
{
    /// <summary>The user has not confirmed yet; poll again after the interval.</summary>
    public sealed record Pending : DevicePollResult;

    /// <summary>The device code is used up or expired; the login has to start over.</summary>
    public sealed record Expired : DevicePollResult;

    public sealed record Authorized(TwitchToken Token) : DevicePollResult;
}

/// <summary>What Twitch reports about a valid access token.</summary>
public sealed record TokenValidation(string UserId, string Login, IReadOnlyList<string> Scopes);
