using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PokeArena.Core.Twitch.Auth;

/// <summary>
/// Device code flow, token refresh and validation against id.twitch.tv for a public client (no client secret).
/// Stateless: storing tokens is <see cref="TwitchTokenProvider"/>'s job.
/// </summary>
public sealed class TwitchAuthClient(HttpClient http, TwitchOptions options, TimeProvider time)
{
    private const string BaseUrl = "https://id.twitch.tv/oauth2/";
    private const string DeviceGrantType = "urn:ietf:params:oauth:grant-type:device_code";

    public async Task<DeviceAuthorization> StartDeviceLoginAsync(CancellationToken ct)
    {
        using var response = await http.PostAsync(
            BaseUrl + "device",
            Form(("client_id", options.ClientId), ("scopes", TwitchScopes.Joined)),
            ct);
        await TwitchJson.EnsureSuccessAsync(response, ct);

        var body = await ReadAsync<DeviceResponse>(response, ct);
        return new DeviceAuthorization(
            body.DeviceCode,
            body.UserCode,
            new Uri(body.VerificationUri),
            time.GetUtcNow().AddSeconds(body.ExpiresIn),
            TimeSpan.FromSeconds(Math.Max(1, body.Interval)));
    }

    public async Task<DevicePollResult> PollDeviceLoginAsync(DeviceAuthorization login, CancellationToken ct)
    {
        using var response = await http.PostAsync(
            BaseUrl + "token",
            Form(
                ("client_id", options.ClientId),
                ("scopes", TwitchScopes.Joined),
                ("device_code", login.DeviceCode),
                ("grant_type", DeviceGrantType)),
            ct);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var message = await TwitchJson.ReadErrorMessageAsync(response, ct);
            return message switch
            {
                "authorization_pending" => new DevicePollResult.Pending(),
                "invalid device code" => new DevicePollResult.Expired(),
                _ => throw new TwitchApiException(response.StatusCode, message),
            };
        }

        await TwitchJson.EnsureSuccessAsync(response, ct);
        return new DevicePollResult.Authorized(await ReadTokenAsync(response, ct));
    }

    /// <exception cref="TwitchAuthRequiredException">The refresh token was rejected (used, revoked or expired).</exception>
    public async Task<TwitchToken> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        using var response = await http.PostAsync(
            BaseUrl + "token",
            Form(
                ("client_id", options.ClientId),
                ("grant_type", "refresh_token"),
                ("refresh_token", refreshToken)),
            ct);

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
        {
            var message = await TwitchJson.ReadErrorMessageAsync(response, ct);
            throw new TwitchAuthRequiredException($"Refresh token rejected: {message}");
        }

        await TwitchJson.EnsureSuccessAsync(response, ct);
        return await ReadTokenAsync(response, ct);
    }

    /// <returns>Details of the token, or null if Twitch no longer accepts it.</returns>
    public async Task<TokenValidation?> ValidateAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + "validate");
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
        using var response = await http.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        await TwitchJson.EnsureSuccessAsync(response, ct);
        var body = await ReadAsync<ValidateResponse>(response, ct);
        return new TokenValidation(body.UserId, body.Login, body.Scopes ?? []);
    }

    private async Task<TwitchToken> ReadTokenAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await ReadAsync<TokenResponse>(response, ct);
        return new TwitchToken(body.AccessToken, body.RefreshToken, time.GetUtcNow().AddSeconds(body.ExpiresIn));
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<T>(TwitchJson.Options, ct)
        ?? throw new JsonException($"Empty {typeof(T).Name} from Twitch");

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields) =>
        new(fields.Select(f => KeyValuePair.Create(f.Key, f.Value)));

    private sealed record DeviceResponse(string DeviceCode, string UserCode, string VerificationUri, int ExpiresIn, int Interval);

    private sealed record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);

    private sealed record ValidateResponse(string UserId, string Login, IReadOnlyList<string>? Scopes);
}
