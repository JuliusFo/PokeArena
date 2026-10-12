using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PokeArena.Core.Twitch.Auth;

namespace PokeArena.Core.Twitch.Helix;

/// <summary>The few Helix endpoints the bot needs, called with the bot account's user token.</summary>
public sealed class HelixClient(HttpClient http, TwitchOptions options, TwitchTokenProvider tokens)
{
    private const string BaseUrl = "https://api.twitch.tv/helix/";

    /// <returns>The user, or null if no account has this login.</returns>
    public async Task<TwitchUser?> GetUserByLoginAsync(string login, CancellationToken ct)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, BaseUrl + "users?login=" + Uri.EscapeDataString(login)),
            ct);
        var body = await ReadAsync<DataResponse<UserData>>(response, ct);
        return body.Data.Select(u => new TwitchUser(u.Id, u.Login, u.DisplayName)).FirstOrDefault();
    }

    /// <summary>Subscribes the WebSocket session to the channel's chat messages as seen by the bot account.</summary>
    public async Task SubscribeToChatMessagesAsync(string sessionId, string broadcasterId, string botUserId, CancellationToken ct)
    {
        var subscription = new
        {
            type = "channel.chat.message",
            version = "1",
            condition = new { broadcaster_user_id = broadcasterId, user_id = botUserId },
            transport = new { method = "websocket", session_id = sessionId },
        };
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, BaseUrl + "eventsub/subscriptions")
            {
                Content = JsonContent.Create(subscription),
            },
            ct);
    }

    public async Task<SendChatResult> SendChatMessageAsync(string broadcasterId, string senderId, string message, CancellationToken ct)
    {
        var request = new { broadcaster_id = broadcasterId, sender_id = senderId, message };
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, BaseUrl + "chat/messages")
            {
                Content = JsonContent.Create(request),
            },
            ct);
        var body = await ReadAsync<DataResponse<SendData>>(response, ct);
        var sent = body.Data.FirstOrDefault() ?? throw new JsonException("Empty send result from Twitch");
        return new SendChatResult(sent.IsSent, sent.DropReason?.Message);
    }

    /// <summary>Sends with the current token; on 401 refreshes once and repeats the request.</summary>
    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> createRequest, CancellationToken ct)
    {
        var accessToken = await tokens.GetAccessTokenAsync(ct);
        var response = await SendOnceAsync(createRequest, accessToken, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            accessToken = await tokens.RefreshAsync(accessToken, ct);
            response = await SendOnceAsync(createRequest, accessToken, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                throw new TwitchAuthRequiredException("Twitch rejects even a freshly refreshed token");
            }
        }

        try
        {
            await TwitchJson.EnsureSuccessAsync(response, ct);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        Func<HttpRequestMessage> createRequest,
        string accessToken,
        CancellationToken ct)
    {
        using var request = createRequest();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Client-Id", options.ClientId);
        return await http.SendAsync(request, ct);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<T>(TwitchJson.Options, ct)
        ?? throw new JsonException($"Empty {typeof(T).Name} from Twitch");

    private sealed record DataResponse<T>(IReadOnlyList<T> Data);

    private sealed record UserData(string Id, string Login, string DisplayName);

    private sealed record SendData(bool IsSent, DropReasonData? DropReason);

    private sealed record DropReasonData(string? Code, string? Message);
}

public sealed record TwitchUser(string Id, string Login, string DisplayName);

/// <param name="DropReason">Why Twitch dropped the message (e.g. AutoMod, duplicate), if it did.</param>
public sealed record SendChatResult(bool IsSent, string? DropReason);
