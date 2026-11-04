using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PokeArena.Core;

/// <summary>
/// Asks GitHub once per app start whether a newer release exists (WP-08). Only a hint for the streamer — nothing is
/// downloaded or installed. Any failure (offline, rate limit) just means no hint.
/// </summary>
public sealed class UpdateCheck(HttpClient http, AppInfo app, ILogger<UpdateCheck> log) : BackgroundService
{
    public const string ReleasesPage = "https://github.com/JuliusFo/PokeArena/releases/latest";

    internal const string LatestReleaseApi = "https://api.github.com/repos/JuliusFo/PokeArena/releases/latest";

    /// <summary>Raised once the check found a newer version.</summary>
    public event Action? Changed;

    /// <summary>The newer version, e.g. "1.1.0", or null if this one is current or the check failed.</summary>
    public string? NewerVersion { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            // GitHub refuses requests without a user agent; a generic product name, nothing about the user.
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PokeArena", app.Version));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await http.SendAsync(request, stoppingToken);
            if (!response.IsSuccessStatusCode)
            {
                log.LogInformation("Update check: GitHub answered {Status}", (int)response.StatusCode);
                return;
            }

            var release = await response.Content.ReadFromJsonAsync<Release>(stoppingToken);
            if (IsNewer(release?.TagName, app.Version))
            {
                NewerVersion = release!.TagName!.TrimStart('v');
                log.LogInformation("Update check: version {Version} is available", NewerVersion);
                Changed?.Invoke();
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            log.LogInformation(ex, "Update check failed");
        }
    }

    internal static bool IsNewer(string? tag, string current) =>
        Version.TryParse(tag?.TrimStart('v'), out var latest)
        && Version.TryParse(current, out var running)
        && latest > running;

    private sealed record Release([property: JsonPropertyName("tag_name")] string? TagName);
}
