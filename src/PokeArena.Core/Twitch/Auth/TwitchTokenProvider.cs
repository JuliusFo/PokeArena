using System.Text.Json;
using Microsoft.Extensions.Logging;
using PokeArena.Core.Secrets;

namespace PokeArena.Core.Twitch.Auth;

/// <summary>
/// Owns the bot account's tokens: loads them from the secret store, hands out a valid access token and refreshes
/// it. Refreshes are serialized because refresh tokens are single-use — two concurrent refreshes would
/// invalidate each other and force a new login.
/// </summary>
public sealed class TwitchTokenProvider(
    TwitchAuthClient auth,
    ISecretStore secrets,
    TimeProvider time,
    ILogger<TwitchTokenProvider> log)
{
    public const string SecretName = "twitch-token";

    /// <summary>Refresh this long before the access token expires, so no request runs into the expiry.</summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private TwitchToken? _token;
    private bool _loaded;

    public bool HasToken => Current is not null;

    private TwitchToken? Current
    {
        get
        {
            if (!_loaded)
            {
                _token = Load();
                _loaded = true;
            }

            return _token;
        }
    }

    /// <summary>Stores the tokens of a fresh device login.</summary>
    public void Save(TwitchToken token)
    {
        secrets.Set(SecretName, JsonSerializer.Serialize(token));
        _token = token;
        _loaded = true;
    }

    /// <summary>Forgets the login (sign out, or Twitch rejected it).</summary>
    public void Clear()
    {
        secrets.Delete(SecretName);
        _token = null;
        _loaded = true;
    }

    /// <exception cref="TwitchAuthRequiredException">No login stored, or it can no longer be refreshed.</exception>
    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var token = Current ?? throw new TwitchAuthRequiredException("No Twitch login stored");
        return token.ExpiresAt - time.GetUtcNow() > RefreshMargin
            ? token.AccessToken
            : await RefreshAsync(token.AccessToken, ct);
    }

    /// <summary>
    /// Replaces <paramref name="rejectedAccessToken"/> with a fresh one. If another caller already refreshed it in
    /// the meantime, returns that token instead of spending the refresh token again.
    /// </summary>
    /// <exception cref="TwitchAuthRequiredException">Twitch rejected the refresh token; the login is cleared.</exception>
    public async Task<string> RefreshAsync(string rejectedAccessToken, CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            var token = Current ?? throw new TwitchAuthRequiredException("No Twitch login stored");
            if (token.AccessToken != rejectedAccessToken && token.ExpiresAt - time.GetUtcNow() > RefreshMargin)
            {
                return token.AccessToken;
            }

            TwitchToken refreshed;
            try
            {
                refreshed = await auth.RefreshAsync(token.RefreshToken, ct);
            }
            catch (TwitchAuthRequiredException)
            {
                log.LogWarning("Twitch rejected the refresh token, a new login is needed");
                Clear();
                throw;
            }

            Save(refreshed);
            log.LogInformation("Twitch access token refreshed, valid until {ExpiresAt:u}", refreshed.ExpiresAt);
            return refreshed.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Checks the login with Twitch (required at startup and hourly) and refreshes once if the access token was
    /// rejected. A login that lacks one of <see cref="TwitchScopes.Bot"/> counts as invalid.
    /// </summary>
    /// <exception cref="TwitchAuthRequiredException">The login is gone or incomplete; it is cleared.</exception>
    public async Task<TokenValidation> ValidateAsync(CancellationToken ct)
    {
        var accessToken = await GetAccessTokenAsync(ct);
        var validation = await auth.ValidateAsync(accessToken, ct);
        if (validation is null)
        {
            accessToken = await RefreshAsync(accessToken, ct);
            validation = await auth.ValidateAsync(accessToken, ct);
        }

        if (validation is null)
        {
            Clear();
            throw new TwitchAuthRequiredException("Twitch does not accept the refreshed token");
        }

        var missing = TwitchScopes.Bot.Except(validation.Scopes).ToList();
        if (missing.Count > 0)
        {
            Clear();
            throw new TwitchAuthRequiredException($"Login lacks scopes: {string.Join(", ", missing)}");
        }

        return validation;
    }

    private TwitchToken? Load()
    {
        var json = secrets.Get(SecretName);
        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TwitchToken>(json);
        }
        catch (JsonException ex)
        {
            log.LogWarning(ex, "Stored Twitch login is unreadable and is ignored");
            return null;
        }
    }
}
