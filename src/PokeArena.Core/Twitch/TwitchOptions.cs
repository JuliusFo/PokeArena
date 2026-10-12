namespace PokeArena.Core.Twitch;

/// <summary>
/// Static Twitch settings from the "PokeArena:Twitch" section. Nothing here is secret; tokens live in the
/// <see cref="Secrets.ISecretStore"/> (ADR-0002).
/// </summary>
public sealed class TwitchOptions
{
    public const string SectionName = "PokeArena:Twitch";

    /// <summary>Client ID of the registered Twitch app (public client, ADR-0002 addendum).</summary>
    public string ClientId { get; set; } = "";

    /// <summary>Login name of the channel the bot plays in. Empty: not configured yet.</summary>
    public string Channel { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(Channel);
}

/// <summary>Scopes the bot account grants during the device login.</summary>
public static class TwitchScopes
{
    /// <summary>
    /// Read and write chat with a user token. <c>user:bot</c> is not needed for that, but keeps the door open
    /// for an app access token later (chat bot badge) without a new login.
    /// </summary>
    public static readonly IReadOnlyList<string> Bot = ["user:read:chat", "user:write:chat", "user:bot"];

    public static string Joined => string.Join(' ', Bot);
}
