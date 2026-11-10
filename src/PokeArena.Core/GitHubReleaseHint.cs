using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PokeArena.Core;

/// <summary>
/// For a copy that cannot update itself (unpacked from the old ZIP, or a developer build): asks GitHub for the latest
/// release, so the top bar can point to the download (WP-08). Nothing is downloaded or installed.
/// </summary>
public sealed class GitHubReleaseHint(HttpClient http, AppInfo app) : IAppUpdater
{
    internal const string LatestReleaseApi = "https://api.github.com/repos/JuliusFo/PokeArena/releases/latest";

    public bool CanSelfUpdate => false;

    public async Task<string?> FindNewerAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        // GitHub refuses requests without a user agent; a generic product name, nothing about the user.
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PokeArena", app.Version));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var release = await response.Content.ReadFromJsonAsync<Release>(ct);
        return IsNewer(release?.TagName, app.Version) ? release!.TagName!.TrimStart('v') : null;
    }

    public Task DownloadAsync(CancellationToken ct) => throw new NotSupportedException("This copy cannot update itself.");

    public void RestartIntoUpdate() => throw new NotSupportedException("This copy cannot update itself.");

    internal static bool IsNewer(string? tag, string current) =>
        Version.TryParse(tag?.TrimStart('v'), out var latest)
        && Version.TryParse(current, out var running)
        && latest > running;

    private sealed record Release([property: JsonPropertyName("tag_name")] string? TagName);
}
