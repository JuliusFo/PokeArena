using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PokeArena.Contracts;
using PokeArena.Core.Data;
using PokeArena.Core.Publishing;
using PokeArena.Core.Tests.Fakes;
using PokeArena.Core.Twitch;
using PokeArena.Core.Twitch.Auth;
using PokeArena.Core.Twitch.Helix;

namespace PokeArena.Core.Tests.Publishing;

public class ProfileImageRefresherTests : IDisposable
{
    private const string Users = "https://api.twitch.tv/helix/users?";
    private const string Cdn = "https://static-cdn.jtvnw.net/jtv_user_pictures/";
    private const string AshPng = Cdn + "ash-profile_image-300x300.png";
    private const string AshPngSmall = Cdn + "ash-profile_image-150x150.png";

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 4, 5, 6];

    private readonly GameFixture _game = new();
    private readonly FakeHttpHandler _http = new();
    private readonly FakeSitePublisher _bucket = new();
    private readonly FakeTwitch _twitch = new();
    private readonly RecordingLogger _log = new();
    private readonly ProfileImageRefresher _refresher;
    private readonly SitePublishWorker _worker;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private (string Id, string Url)[] _reported = [];
    private Action? _onHelix;

    public ProfileImageRefresherTests()
    {
        _http.On(HttpMethod.Get, Users, HelixUsers);
        var options = new TwitchOptions { ClientId = "client-1", Channel = "#ShiyoFurball" };
        var http = new HttpClient(_http);
        var tokens = new TwitchTokenProvider(new TwitchAuthClient(http, options, _game.Time), new FakeSecretStore(), _game.Time, NullLogger<TwitchTokenProvider>.Instance);
        tokens.Save(new TwitchToken("at-1", "rt-1", _game.Time.GetUtcNow().AddDays(30)));
        _refresher = new ProfileImageRefresher(
            _game.Database,
            new HelixClient(http, options, tokens),
            http,
            _twitch,
            _game.Time,
            _log);
        _worker = new SitePublishWorker(_game.Database, new SiteGenerator(GameFixture.Species, GameFixture.Npcs), _bucket, _game.Time, NullLogger<SitePublishWorker>.Instance);
    }

    private static Species Starter => GameFixture.Species.Starters[0];

    [Fact]
    public async Task A_new_trainer_gets_the_small_picture_on_the_website()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        TwitchReports(("1", AshPng));
        _http.OnBytes(AshPngSmall, Png, "image/png");

        await RefreshAndPublishAsync();

        var file = _bucket.Files[SitePaths.ProfileImage("1", SitePaths.Png)];
        Assert.Equal(Png, file.Content);
        Assert.Equal((SitePaths.Png, SiteGenerator.ImageCache), (file.ContentType, file.CacheControl));
        Assert.Equal("profilbild/1.png", Trainer("1").ProfileImage);
        Assert.Empty(_http.RequestsTo(AshPng));
    }

    [Fact]
    public async Task A_new_or_lost_picture_republishes_the_leaderboard()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        await _worker.PublishDueAsync(_ct);
        Assert.Null(LeaderboardEntry("1").ProfileImage);

        TwitchReports(("1", AshPng));
        _http.OnBytes(AshPngSmall, Png, "image/png");
        await RefreshAndPublishAsync();
        Assert.Equal("profilbild/1.png", LeaderboardEntry("1").ProfileImage);

        _game.Time.Advance(TimeSpan.FromDays(1));
        TwitchReports();
        await RefreshAndPublishAsync();
        Assert.Null(LeaderboardEntry("1").ProfileImage);
    }

    [Fact]
    public async Task Without_a_small_variant_the_reported_picture_is_taken()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        TwitchReports(("1", AshPng));
        _http.OnBytes(AshPngSmall, [], "text/plain", HttpStatusCode.NotFound);
        _http.OnBytes(AshPng, Png, "image/png");

        await RefreshAndPublishAsync();

        Assert.Equal(Png, _bucket.Files[SitePaths.ProfileImage("1", SitePaths.Png)].Content);
    }

    [Fact]
    public async Task A_current_picture_is_checked_once_a_day_and_downloaded_only_when_its_url_changes()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        TwitchReports(("1", AshPng));
        _http.OnBytes(AshPngSmall, Png, "image/png");
        await RefreshAndPublishAsync();

        _game.Time.Advance(TimeSpan.FromHours(23));
        await _refresher.RefreshDueAsync(_ct);
        Assert.Single(_http.RequestsTo(Users));

        _game.Time.Advance(TimeSpan.FromHours(2));
        await _refresher.RefreshDueAsync(_ct);
        Assert.Equal(2, _http.RequestsTo(Users).Count());
        Assert.Single(_http.RequestsTo(Cdn));
        await using var db = _game.OpenLive();
        Assert.Empty(db.PublishQueue);
    }

    [Fact]
    public async Task A_new_picture_in_another_format_replaces_the_old_file()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        TwitchReports(("1", AshPng));
        _http.OnBytes(AshPngSmall, Png, "image/png");
        await RefreshAndPublishAsync();

        _game.Time.Advance(TimeSpan.FromDays(1));
        TwitchReports(("1", Cdn + "new-profile_image-300x300.jpeg"));
        _http.OnBytes(Cdn + "new-profile_image-150x150.jpeg", Jpeg, "image/jpeg");
        await RefreshAndPublishAsync();

        Assert.DoesNotContain(SitePaths.ProfileImage("1", SitePaths.Png), _bucket.Files.Keys);
        Assert.Equal(Jpeg, _bucket.Files[SitePaths.ProfileImage("1", SitePaths.Jpeg)].Content);
        Assert.Equal("profilbild/1.jpg", Trainer("1").ProfileImage);
    }

    [Fact]
    public async Task An_account_twitch_no_longer_knows_loses_its_picture_but_stays_a_trainer()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        TwitchReports(("1", AshPng));
        _http.OnBytes(AshPngSmall, Png, "image/png");
        await RefreshAndPublishAsync();

        _game.Time.Advance(TimeSpan.FromDays(1));
        TwitchReports();
        await RefreshAndPublishAsync();

        Assert.DoesNotContain(SitePaths.ProfileImage("1", SitePaths.Png), _bucket.Files.Keys);
        Assert.Null(Trainer("1").ProfileImage);
    }

    [Theory]
    [InlineData("https://evil.example/ash-profile_image-300x300.png", "image/png", 10)]
    [InlineData("http://static-cdn.jtvnw.net/ash-profile_image-300x300.png", "image/png", 10)]
    [InlineData(AshPng, "text/html", 10)]
    [InlineData(AshPng, "image/svg+xml", 10)]
    [InlineData(AshPng, "image/png", 10)]
    [InlineData(AshPng, "image/png", ProfileImageRefresher.MaxBytes + 1)]
    public async Task Pictures_from_elsewhere_of_another_kind_or_too_large_are_refused(string url, string contentType, int size)
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        TwitchReports(("1", url));
        _http.OnBytes(url.Replace("-300x300.", "-150x150.", StringComparison.Ordinal), new byte[size], contentType);
        _http.OnBytes(url, new byte[size], contentType);

        await RefreshAndPublishAsync();

        await using var db = _game.OpenLive();
        Assert.Empty(db.ProfileImages);
        Assert.Null(Trainer("1").ProfileImage);
        if (!url.StartsWith("https://static-cdn.jtvnw.net/", StringComparison.Ordinal))
        {
            Assert.Equal(_http.RequestsTo(Users).Count(), _http.Requests.Count);
        }

        // The log names the trainer by id; picture URLs can contain the login (privacy page).
        Assert.NotEmpty(_log.Messages);
        Assert.All(_log.Messages, m => Assert.DoesNotContain("profile_image", m, StringComparison.Ordinal));
        Assert.Contains(_log.Messages, m => m.Contains("of 1 ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_picture_is_typed_by_its_bytes_not_by_the_header()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        TwitchReports(("1", AshPng));
        _http.OnBytes(AshPngSmall, Png, "image/jpeg");

        await RefreshAndPublishAsync();

        Assert.Equal(Png, _bucket.Files[SitePaths.ProfileImage("1", SitePaths.Png)].Content);
        Assert.Equal("profilbild/1.png", Trainer("1").ProfileImage);
    }

    [Fact]
    public async Task A_picture_redirected_away_from_twitch_is_refused()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        TwitchReports(("1", AshPng));
        _http.On(HttpMethod.Get, Cdn, () => Picture(Png, "https://evil.example/ash.png"));

        await RefreshAndPublishAsync();

        await using var db = _game.OpenLive();
        Assert.Empty(db.ProfileImages);
        Assert.Equal(2, _http.RequestsTo(Cdn).Count());
    }

    [Fact]
    public async Task A_picture_whose_body_stalls_is_given_up_after_the_download_timeout()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        TwitchReports(("1", AshPng));
        _http.On(HttpMethod.Get, AshPngSmall, () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StallingContent() });
        _http.OnBytes(AshPng, Png, "image/png");

        var round = _refresher.RefreshDueAsync(_ct);
        await Eventually.TrueAsync(() => _http.RequestsTo(AshPngSmall).Any());
        _game.Time.Advance(ProfileImageRefresher.DownloadTimeout);
        await round.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, _ct);
        await _worker.PublishDueAsync(_ct);

        Assert.Equal(Png, _bucket.Files[SitePaths.ProfileImage("1", SitePaths.Png)].Content);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_trainer_who_got_no_picture_is_asked_for_again_only_after_a_day(bool accountGone)
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        if (accountGone)
        {
            TwitchReports();
        }
        else
        {
            TwitchReports(("1", AshPng));
            _http.OnBytes(Cdn, [1, 2, 3], "text/html");
        }

        await _refresher.RefreshDueAsync(_ct);
        _game.Time.Advance(ProfileImageRefresher.Interval);
        await _refresher.RefreshDueAsync(_ct);
        Assert.Single(_http.RequestsTo(Users));

        _game.Time.Advance(ProfileImageRefresher.MaxAge);
        await _refresher.RefreshDueAsync(_ct);
        Assert.Equal(2, _http.RequestsTo(Users).Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task An_unreachable_picture_is_tried_again_next_round(int status)
    {
        // 0: no connection at all; otherwise the CDN answers with a passing error.
        Func<HttpResponseMessage> unreachable = status == 0
            ? () => throw new HttpRequestException("offline")
            : () => new HttpResponseMessage((HttpStatusCode)status);
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        TwitchReports(("1", AshPng));
        _http.On(HttpMethod.Get, AshPngSmall, unreachable);
        _http.OnBytes(AshPngSmall, Png, "image/png");
        _http.On(HttpMethod.Get, AshPng, unreachable);

        await RefreshAndPublishAsync();
        Assert.DoesNotContain(SitePaths.ProfileImage("1", SitePaths.Png), _bucket.Files.Keys);

        _game.Time.Advance(ProfileImageRefresher.Interval);
        await RefreshAndPublishAsync();
        Assert.Equal(Png, _bucket.Files[SitePaths.ProfileImage("1", SitePaths.Png)].Content);
    }

    [Fact]
    public async Task A_trainer_deleted_during_a_round_fails_only_that_round()
    {
        await using (var db = _game.OpenLive())
        {
            db.Trainers.AddRange(
                new Trainer { TwitchUserId = "1", DisplayName = "Ash", Login = "ash" },
                new Trainer { TwitchUserId = "2", DisplayName = "Misty", Login = "misty" });
            await db.SaveChangesAsync(_ct);
        }

        // Twitch no longer knows 2: that puts it on the day's wait — but only once the round is stored.
        TwitchReports(("1", AshPng));
        _http.OnBytes(Cdn, Png, "image/png");
        _onHelix = () =>
        {
            // Deleted while Helix answers: storing the batch then breaks the foreign key.
            using var db = _game.OpenLive();
            db.Database.ExecuteSqlRaw("DELETE FROM Trainers WHERE TwitchUserId = '1'");
        };

        await _refresher.RefreshDueAsync(_ct);
        await using (var db = _game.OpenLive())
        {
            Assert.Empty(db.ProfileImages);
            Assert.Empty(db.PublishQueue);
        }

        _onHelix = null;
        _game.Time.Advance(ProfileImageRefresher.Interval);
        await _refresher.RefreshDueAsync(_ct);
        Assert.EndsWith("id=2", _http.RequestsTo(Users).Last().Url, StringComparison.Ordinal);

        // Stored this time, so 2 now waits a day.
        _game.Time.Advance(ProfileImageRefresher.Interval);
        await _refresher.RefreshDueAsync(_ct);
        Assert.Equal(2, _http.RequestsTo(Users).Count());
    }

    [Fact]
    public async Task Rounds_run_only_while_twitch_is_connected_and_every_fifteen_minutes()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        TwitchReports(("1", AshPng));
        _http.On(HttpMethod.Get, Cdn, () => throw new HttpRequestException("offline"));
        _twitch.State = new TwitchConnectionState(TwitchStatus.Disconnected);
        await _refresher.StartAsync(_ct);
        try
        {
            for (var i = 0; i < 3; i++)
            {
                _game.Time.Advance(ProfileImageRefresher.CheckInterval);
                await Task.Delay(20, _ct);
            }

            Assert.Empty(_http.RequestsTo(Users));

            // Advanced until the first round ran: an advance before the loop waits again would be lost.
            _twitch.State = new TwitchConnectionState(TwitchStatus.Connected);
            for (var i = 0; i < 5 && _http.RequestsTo(Cdn).Count() < 2; i++)
            {
                _game.Time.Advance(ProfileImageRefresher.CheckInterval);
                await Task.Delay(20, _ct);
            }

            await Eventually.TrueAsync(() => _http.RequestsTo(Cdn).Count() == 2, "first round once connected");

            // A slow first round only pushes the second one later, never earlier.
            var minutes = 0;
            while (_http.RequestsTo(Users).Count() < 2 && minutes < 30)
            {
                _game.Time.Advance(ProfileImageRefresher.CheckInterval);
                minutes++;
                await Task.Delay(20, _ct);
            }

            Assert.Equal(2, _http.RequestsTo(Users).Count());
            Assert.InRange(minutes, (int)ProfileImageRefresher.Interval.TotalMinutes, 30);
        }
        finally
        {
            await _refresher.StopAsync(_ct);
        }
    }

    [Fact]
    public async Task Trainers_are_asked_for_in_batches_of_one_hundred()
    {
        await using (var db = _game.OpenLive())
        {
            for (var i = 1; i <= 150; i++)
            {
                db.Trainers.Add(new Trainer { TwitchUserId = $"{i}", DisplayName = $"T{i}", Login = $"t{i}" });
            }

            await db.SaveChangesAsync(_ct);
        }

        TwitchReports();

        await _refresher.RefreshDueAsync(_ct);

        var requests = _http.RequestsTo(Users).ToList();
        Assert.Equal([100, 50], requests.Select(r => r.Url.Split('&').Length));
        Assert.Equal(["Bearer at-1", "Bearer at-1"], requests.Select(r => r.Authorization));
    }

    [Fact]
    public async Task Deleting_a_trainer_deletes_the_picture()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        TwitchReports(("1", AshPng));
        _http.OnBytes(AshPngSmall, Png, "image/png");
        await RefreshAndPublishAsync();

        await _game.Trainers.DeleteAsync("1", _ct);
        await _worker.PublishDueAsync(_ct);

        Assert.DoesNotContain(_bucket.Files.Keys, k => k.StartsWith(SitePaths.ProfileImagePrefix, StringComparison.Ordinal));
        await using var db = _game.OpenLive();
        Assert.Empty(db.ProfileImages);
    }

    [Fact]
    public async Task Republishing_uploads_every_picture_and_removes_leftovers()
    {
        await _game.Trainers.RegisterAsync("1", "ash", "Ash", Starter, _ct);
        TwitchReports(("1", AshPng));
        _http.OnBytes(AshPngSmall, Png, "image/png");
        await RefreshAndPublishAsync();
        _bucket.Files.TryRemove(SitePaths.ProfileImage("1", SitePaths.Png), out _);
        _bucket.Files["profilbild/9.png"] = new SiteFile([], SiteGenerator.ImageCache);
        _bucket.Files["profilbild/1.jpg"] = new SiteFile([], SiteGenerator.ImageCache);
        _bucket.Files["profilbild/readme.txt"] = new SiteFile([], SiteGenerator.ImageCache);

        await _worker.RepublishEverythingAsync(_ct);
        await _worker.PublishDueAsync(_ct);

        Assert.Equal(
            ["profilbild/1.png", "profilbild/readme.txt"],
            _bucket.Files.Keys.Where(k => k.StartsWith(SitePaths.ProfileImagePrefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("profilbild/123.png", "123", SitePaths.Png)]
    [InlineData("profilbild/123.jpg", "123", SitePaths.Jpeg)]
    [InlineData("profilbild/.png", null, null)]
    [InlineData("profilbild/12a.png", null, null)]
    [InlineData("profilbild/123.gif", null, null)]
    [InlineData("trainer/123.json", null, null)]
    public void Profile_picture_paths_are_parsed_strictly(string path, string? trainerId, string? contentType)
    {
        var parsed = SitePaths.ProfileImageOf(path);

        Assert.Equal(trainerId, parsed?.TrainerId);
        Assert.Equal(contentType, parsed?.ContentType);
        if (parsed is ({ } id, { } type))
        {
            Assert.Equal(path, SitePaths.ProfileImage(id, type));
        }
    }

    /// <summary>What Helix answers from now on.</summary>
    private void TwitchReports(params (string Id, string Url)[] users) => _reported = users;

    private static HttpResponseMessage Picture(byte[] content, string servedFrom)
    {
        var body = new ByteArrayContent(content);
        body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = body, RequestMessage = new HttpRequestMessage(HttpMethod.Get, servedFrom) };
    }

    private HttpResponseMessage HelixUsers()
    {
        _onHelix?.Invoke();
        var data = _reported.Select(u => new { id = u.Id, login = "u" + u.Id, display_name = "U" + u.Id, profile_image_url = u.Url });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { data }), System.Text.Encoding.UTF8, "application/json") };
    }

    private async Task RefreshAndPublishAsync()
    {
        await _refresher.RefreshDueAsync(_ct);
        await _worker.PublishDueAsync(_ct);
    }

    private TrainerPage Trainer(string id) =>
        JsonSerializer.Deserialize(_bucket.Files[SitePaths.Trainer(id)].Content, SiteJson.Default.TrainerPage)!;

    private LeaderboardEntry LeaderboardEntry(string id) =>
        JsonSerializer.Deserialize(_bucket.Files[SitePaths.Leaderboard].Content, SiteJson.Default.Leaderboard)!.Entries.Single(e => e.TrainerId == id);

    public void Dispose() => _game.Dispose();

    private sealed class FakeTwitch : ITwitchConnection
    {
        public TwitchConnectionState State { get; set; } = new(TwitchStatus.Connected);

        public string Channel => "shiyofurball";

        public event Action? StateChanged
        {
            add { }
            remove { }
        }

        public Task BeginLoginAsync() => Task.CompletedTask;

        public void CancelLogin()
        {
        }

        public void Connect()
        {
        }

        public Task DisconnectAsync() => Task.CompletedTask;

        public Task SignOutAsync() => Task.CompletedTask;
    }

    private sealed class RecordingLogger : ILogger<ProfileImageRefresher>
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyCollection<string> Messages => _messages;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _messages.Enqueue(formatter(state, exception) + (exception is null ? "" : " " + exception));
    }

    /// <summary>Response headers arrive, the body never does.</summary>
    private sealed class StallingContent : HttpContent
    {
        public StallingContent() => Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new StallingStream());

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.Delay(Timeout.Infinite);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class StallingStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
