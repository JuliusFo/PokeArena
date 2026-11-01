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

    public Task<SiteMeta?> MetaAsync(CancellationToken ct) => GetAsync(SitePaths.Meta, SiteJson.Default.SiteMeta, ct);
}
