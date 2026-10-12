using System.Net.Http.Json;
using System.Text.Json;

namespace PokeArena.Core.Twitch;

/// <summary>JSON conventions of the Twitch APIs (snake_case) and their error bodies.</summary>
internal static class TwitchJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <exception cref="TwitchApiException">The response has an error status.</exception>
    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new TwitchApiException(response.StatusCode, await ReadErrorMessageAsync(response, ct));
        }
    }

    /// <summary>The "message" of a Twitch error body, or the raw status text if the body is not the usual JSON.</summary>
    public static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(Options, ct);
            if (!string.IsNullOrWhiteSpace(error?.Message))
            {
                return error.Message;
            }
        }
        catch (JsonException)
        {
            // Not JSON (e.g. a proxy error page): fall through to the status text.
        }

        return response.ReasonPhrase ?? response.StatusCode.ToString();
    }

    private sealed record ErrorResponse(string? Message);
}
