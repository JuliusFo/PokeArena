using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PokeArena.Contracts;
using PokeArena.Core.Data;
using PokeArena.Core.Twitch;
using PokeArena.Core.Twitch.Helix;

namespace PokeArena.Core.Publishing;

/// <summary>
/// Mirrors the trainers' Twitch profile pictures to the website (feat/trainer-profile-image): asks Helix for the
/// picture URLs of trainers without a picture or with one not checked for <see cref="MaxAge"/>, downloads only the
/// pictures whose URL changed, stores the bytes in the live database and queues the picture and the trainer page.
/// Runs every <see cref="Interval"/> while Twitch is connected; the dry run's sandbox is never touched.
/// </summary>
public sealed class ProfileImageRefresher(
    GameDatabase database,
    HelixClient helix,
    HttpClient http,
    ITwitchConnection twitch,
    TimeProvider time,
    ILogger<ProfileImageRefresher> log) : BackgroundService
{
    /// <summary>Pause between two rounds; a new trainer has a picture this long after registering at the latest.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    /// <summary>How often Twitch is asked whether a stored picture is still current.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    /// <summary>How often the service looks whether a round is due and Twitch is connected.</summary>
    internal static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    /// <summary>Larger pictures are refused; Twitch's 150×150 pictures are far below.</summary>
    public const int MaxBytes = 512 * 1024;

    /// <summary>The only host pictures are downloaded from: Twitch's CDN.</summary>
    public const string ImageHost = "static-cdn.jtvnw.net";

    /// <summary>Twitch reports the 300×300 variant; the page shows about 56 px, 150×150 is enough for sharp screens.</summary>
    private const string ReportedSize = "-300x300.";
    private const string WantedSize = "-150x150.";

    /// <summary>One round: checks every trainer that is due. Public for tests; the service calls it in a loop.</summary>
    public async Task RefreshDueAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var checkedBefore = now - MaxAge;
        List<string> due;
        await using (var db = database.OpenLive())
        {
            due = await db.Trainers.AsNoTracking()
                .Where(t => !db.ProfileImages.Any(i => i.TrainerId == t.TwitchUserId && i.CheckedAt > checkedBefore))
                .Select(t => t.TwitchUserId)
                .OrderBy(id => id)
                .ToListAsync(ct);
        }

        // Only real Twitch ids: anything else would make Helix refuse the whole batch.
        foreach (var batch in due.Where(id => id.Length > 0 && id.All(char.IsAsciiDigit)).Chunk(HelixClient.MaxUsersPerRequest))
        {
            await RefreshAsync(batch, ct);
        }
    }

    private async Task RefreshAsync(IReadOnlyList<string> trainerIds, CancellationToken ct)
    {
        var urls = await helix.GetProfileImageUrlsAsync(trainerIds, ct);
        await using var db = database.OpenLive();
        var stored = await db.ProfileImages.Where(i => trainerIds.Contains(i.TrainerId)).ToDictionaryAsync(i => i.TrainerId, ct);
        var paths = new List<string>();
        var now = time.GetUtcNow();
        foreach (var trainerId in trainerIds)
        {
            stored.TryGetValue(trainerId, out var image);
            if (!urls.TryGetValue(trainerId, out var url))
            {
                // Twitch no longer knows the account: the picture goes, the trainer stays.
                if (image is not null)
                {
                    db.ProfileImages.Remove(image);
                    paths.AddRange([SitePaths.ProfileImage(trainerId, image.ContentType), SitePaths.Trainer(trainerId)]);
                }

                continue;
            }

            if (image?.SourceUrl == url)
            {
                image.CheckedAt = now;
                continue;
            }

            if (await DownloadAsync(url, ct) is not { } download)
            {
                // Kept as it was and tried again next round.
                continue;
            }

            if (image is null)
            {
                db.ProfileImages.Add(new TrainerProfileImage
                {
                    TrainerId = trainerId,
                    Content = download.Content,
                    ContentType = download.ContentType,
                    SourceUrl = url,
                    CheckedAt = now,
                });
            }
            else
            {
                // A format change moves the file: the old path then has no source and gets deleted.
                paths.Add(SitePaths.ProfileImage(trainerId, image.ContentType));
                image.Content = download.Content;
                image.ContentType = download.ContentType;
                image.SourceUrl = url;
                image.CheckedAt = now;
            }

            paths.AddRange([SitePaths.ProfileImage(trainerId, download.ContentType), SitePaths.Trainer(trainerId)]);
        }

        await SiteOutbox.EnqueueAsync(db, paths, now, ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // A trainer was deleted meanwhile; the others are checked again next round.
            log.LogWarning(ex, "Storing profile pictures failed");
            return;
        }

        if (paths.Count > 0)
        {
            log.LogInformation("Profile pictures changed for {Count} trainers", paths.Count(p => SitePaths.TrainerIdOf(p) is not null));
        }
    }

    /// <summary>The picture at <paramref name="url"/> (150×150 if Twitch has it), or null if it is refused or unreachable.</summary>
    private async Task<DownloadedImage?> DownloadAsync(string url, CancellationToken ct)
    {
        if (!IsTwitchImage(url))
        {
            log.LogWarning("Profile picture URL {Url} is not on Twitch's CDN, ignored", url);
            return null;
        }

        var candidates = url.Contains(ReportedSize, StringComparison.Ordinal)
            ? [url.Replace(ReportedSize, WantedSize, StringComparison.Ordinal), url]
            : new[] { url };
        foreach (var candidate in candidates)
        {
            try
            {
                if (await TryDownloadAsync(candidate, ct) is { } image)
                {
                    return image;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException
                || (ex is OperationCanceledException && !ct.IsCancellationRequested))
            {
                log.LogWarning(ex, "Downloading profile picture {Url} failed", candidate);
            }
        }

        return null;
    }

    private async Task<DownloadedImage?> TryDownloadAsync(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            log.LogDebug("Profile picture {Url} answered {Status}", url, (int)response.StatusCode);
            return null;
        }

        // Redirects are followed (the handler reports the last request): the picture must still come from Twitch's CDN.
        if (!IsTwitchImage(response.RequestMessage?.RequestUri?.ToString() ?? url))
        {
            log.LogWarning("Profile picture {Url} was redirected away from Twitch's CDN", url);
            return null;
        }

        var contentType = NormalizedType(response.Content.Headers.ContentType);
        if (contentType is null || response.Content.Headers.ContentLength > MaxBytes)
        {
            log.LogWarning("Profile picture {Url} refused ({Type}, {Length} bytes)", url, response.Content.Headers.ContentType, response.Content.Headers.ContentLength);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBytes)
            {
                log.LogWarning("Profile picture {Url} is larger than {Max} bytes, refused", url, MaxBytes);
                return null;
            }
        }

        return buffer.Length == 0 ? null : new DownloadedImage(buffer.ToArray(), contentType);
    }

    private static bool IsTwitchImage(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && string.Equals(uri.Host, ImageHost, StringComparison.OrdinalIgnoreCase);

    private static string? NormalizedType(MediaTypeHeaderValue? type) => type?.MediaType?.ToLowerInvariant() switch
    {
        SitePaths.Png => SitePaths.Png,
        SitePaths.Jpeg or "image/jpg" => SitePaths.Jpeg,
        _ => null,
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextRound = time.GetUtcNow();
        while (!stoppingToken.IsCancellationRequested)
        {
            if (twitch.State.Status == TwitchStatus.Connected && time.GetUtcNow() >= nextRound)
            {
                try
                {
                    await RefreshDueAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Refreshing profile pictures failed");
                }

                nextRound = time.GetUtcNow() + Interval;
            }

            try
            {
                await Task.Delay(CheckInterval, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private sealed record DownloadedImage(byte[] Content, string ContentType);
}
