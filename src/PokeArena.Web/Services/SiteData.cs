using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using PokeArena.Contracts;

namespace PokeArena.Web.Services;

/// <summary>Loads the site's JSON files from the data host (spec §9).</summary>
public sealed class SiteData(HttpClient http)
{
    /// <summary>The file at <paramref name="path"/>, or null if it does not exist (yet).</summary>
    /// <exception cref="HttpRequestException">The data host is unreachable or answered with an error.</exception>
    public async Task<T?> GetAsync<T>(string path, JsonTypeInfo<T> type, CancellationToken ct)
        where T : class
    {
        using var response = await http.GetAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(type, ct);
    }

    public Task<Leaderboard?> LeaderboardAsync(CancellationToken ct) => GetAsync(SitePaths.Leaderboard, SiteJson.Default.Leaderboard, ct);

    public Task<BattleIndex?> BattleIndexAsync(CancellationToken ct) => GetAsync(SitePaths.BattleIndex, SiteJson.Default.BattleIndex, ct);

    public Task<BattlePage?> BattleAsync(long id, CancellationToken ct) => GetAsync(SitePaths.Battle(id), SiteJson.Default.BattlePage, ct);

    public Task<TrainerPage?> TrainerAsync(string id, CancellationToken ct) => GetAsync(SitePaths.Trainer(id), SiteJson.Default.TrainerPage, ct);

    public Task<Statistics?> StatisticsAsync(CancellationToken ct) => GetAsync(SitePaths.Statistics, SiteJson.Default.Statistics, ct);

    public Task<SiteMeta?> MetaAsync(CancellationToken ct) => GetAsync(SitePaths.Meta, SiteJson.Default.SiteMeta, ct);

    /// <summary>
    /// The address of a trainer's profile picture on the data host, or null without one. Only paths in the bot's own
    /// format count, so a file can never point the page at another host.
    /// </summary>
    public string? ProfileImageUrl(TrainerPage trainer) =>
        trainer.ProfileImage is { } path && SitePaths.ProfileImageOf(path) is not null && http.BaseAddress is { } data
            ? new Uri(data, path).ToString()
            : null;
}
