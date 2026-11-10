using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Core.Tests.Fakes;
using PokeArena.Core.Twitch;

namespace PokeArena.Core.Tests;

public class UpdateCheckTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 10, 20, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Start);
    private readonly FakeUpdater _updater = new();
    private readonly FakeGate _gate = new();
    private readonly UpdateCheck _check;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public UpdateCheckTests()
    {
        _check = new UpdateCheck(_updater, _gate, _time, NullLogger<UpdateCheck>.Instance);
    }

    [Fact]
    public async Task The_first_check_waits_five_minutes_then_downloads_the_newer_version()
    {
        _updater.Newer = "1.6.1";
        _gate.MayInstall = false;
        await _check.StartAsync(_ct);
        await Task.Delay(20, _ct);

        _time.Advance(UpdateCheck.FirstCheckDelay - TimeSpan.FromSeconds(1));
        await Task.Delay(20, _ct);
        Assert.Equal(0, _updater.Checks);

        await PumpUntilAsync(() => _check.State == UpdateState.Ready);
        Assert.Equal("1.6.1", _check.NewerVersion);
        Assert.Equal(1, _updater.Downloads);
        Assert.Equal(0, _updater.Installs);
    }

    [Fact]
    public async Task A_ready_version_waits_for_the_stream_to_end_and_then_installs()
    {
        _updater.Newer = "1.6.1";
        _gate.MayInstall = false;
        await _check.StartAsync(_ct);
        await PumpUntilAsync(() => _check.State == UpdateState.Ready);

        await PumpUntilAsync(() => _time.GetUtcNow() >= Start + TimeSpan.FromMinutes(40));
        Assert.Equal(0, _updater.Installs);

        _gate.MayInstall = true;
        await PumpUntilAsync(() => _updater.Installs == 1);
        Assert.True(_check.Installing);
        await _check.ExecuteTask!;
    }

    [Fact]
    public async Task Checks_repeat_hourly_and_the_same_version_is_not_downloaded_twice()
    {
        _gate.MayInstall = false;
        await _check.StartAsync(_ct);
        await PumpUntilAsync(() => _updater.Checks == 1);
        Assert.Equal(UpdateState.Current, _check.State);

        _updater.Newer = "1.6.1";
        await PumpUntilAsync(() => _check.State == UpdateState.Ready);
        Assert.Equal(2, _updater.Checks);

        await PumpUntilAsync(() => _updater.Checks == 3);
        Assert.Equal(1, _updater.Downloads);
    }

    [Fact]
    public async Task A_failed_download_shows_and_is_retried_at_the_next_check()
    {
        _updater.Newer = "1.6.1";
        _updater.FailDownload = true;
        _gate.MayInstall = false;
        await _check.StartAsync(_ct);
        await PumpUntilAsync(() => _check.State == UpdateState.Failed);

        _updater.FailDownload = false;
        await PumpUntilAsync(() => _check.State == UpdateState.Ready);
        Assert.Equal(2, _updater.Checks);
    }

    [Fact]
    public async Task A_failed_check_keeps_a_version_that_is_already_downloaded()
    {
        _updater.Newer = "1.6.1";
        Assert.Equal(UpdateCheckResult.Newer, await _check.CheckNowAsync(_ct));

        _updater.FailCheck = true;
        Assert.Equal(UpdateCheckResult.Failed, await _check.CheckNowAsync(_ct));

        Assert.Equal(UpdateState.Ready, _check.State);
        Assert.Equal("1.6.1", _check.NewerVersion);
    }

    [Fact]
    public async Task A_copy_that_cannot_update_itself_only_shows_the_hint()
    {
        _updater.SelfUpdate = false;
        _updater.Newer = "1.6.1";

        Assert.Equal(UpdateCheckResult.Newer, await _check.CheckNowAsync(_ct));
        Assert.Equal(UpdateState.Available, _check.State);
        Assert.Equal(0, _updater.Downloads);
        Assert.False(_check.InstallNow());

        // Offline or rate limited: the hint stays, no error shows.
        _updater.FailCheck = true;
        Assert.Equal(UpdateCheckResult.Failed, await _check.CheckNowAsync(_ct));
        Assert.Equal(UpdateState.Available, _check.State);
    }

    [Fact]
    public async Task Checking_by_hand_says_when_the_app_is_current()
    {
        Assert.Equal(UpdateCheckResult.UpToDate, await _check.CheckNowAsync(_ct));
        Assert.Equal(UpdateState.Current, _check.State);
        Assert.Null(_check.NewerVersion);
    }

    [Fact]
    public async Task Installing_by_hand_works_during_a_stream_but_waits_for_an_open_lobby()
    {
        _updater.Newer = "1.6.1";
        _gate.MayInstall = false;
        await _check.CheckNowAsync(_ct);

        _gate.Lobby = true;
        Assert.False(_check.InstallNow());
        Assert.Equal(0, _updater.Installs);

        _gate.Lobby = false;
        Assert.True(_check.InstallNow());
        Assert.False(_check.InstallNow());
        Assert.Equal(1, _updater.Installs);
    }

    [Fact]
    public async Task An_install_that_cannot_start_shows_as_failed()
    {
        _updater.Newer = "1.6.1";
        _updater.FailInstall = true;
        await _check.CheckNowAsync(_ct);

        Assert.False(_check.InstallNow());

        Assert.Equal(UpdateState.Failed, _check.State);
        Assert.False(_check.Installing);
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    public void The_gate_opens_only_while_the_bot_is_idle(bool live, bool dryRun, bool lobby, bool mayInstall)
    {
        var stream = new StreamStatus();
        stream.Report(live);
        var gate = new UpdateGate(stream, new DryRunMode { Enabled = dryRun }, () => lobby);

        Assert.Equal(mayInstall, gate.MayInstallAutomatically);
        Assert.Equal(lobby, gate.LobbyOpen);
    }

    [Fact]
    public void Always_active_keeps_the_gate_closed_while_offline()
    {
        var gate = new UpdateGate(new StreamStatus { AlwaysActive = true }, new DryRunMode(), () => false);

        Assert.False(gate.MayInstallAutomatically);
    }

    /// <summary>
    /// Advances the clock a minute at a time until the condition holds: the background loop registers its next delay
    /// only after the previous one fired, so a single big step could slip past it.
    /// </summary>
    private async Task PumpUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 600 && !condition(); i++)
        {
            _time.Advance(TimeSpan.FromMinutes(1));
            await Task.Delay(2, _ct);
        }

        Assert.True(condition());
    }

    public void Dispose()
    {
        _check.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class FakeUpdater : IAppUpdater
    {
        public bool SelfUpdate { get; set; } = true;
        public string? Newer { get; set; }
        public bool FailCheck { get; set; }
        public bool FailDownload { get; set; }
        public bool FailInstall { get; set; }
        public int Checks { get; private set; }
        public int Downloads { get; private set; }
        public int Installs { get; private set; }

        public bool CanSelfUpdate => SelfUpdate;

        public Task<string?> FindNewerAsync(CancellationToken ct)
        {
            Checks++;
            return FailCheck ? throw new HttpRequestException("offline") : Task.FromResult(Newer);
        }

        public Task DownloadAsync(CancellationToken ct)
        {
            Downloads++;
            return FailDownload ? throw new HttpRequestException("broken download") : Task.CompletedTask;
        }

        public void RestartIntoUpdate()
        {
            if (FailInstall)
            {
                throw new InvalidOperationException("Update.exe missing");
            }

            Installs++;
        }
    }

    private sealed class FakeGate : IUpdateGate
    {
        public bool Lobby { get; set; }
        public bool MayInstall { get; set; } = true;

        public bool LobbyOpen => Lobby;

        public bool MayInstallAutomatically => MayInstall && !Lobby;
    }
}

public class GitHubReleaseHintTests
{
    private readonly FakeHttpHandler _http = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("v1.1.0", "1.0.0", true)]
    [InlineData("v1.0.0", "1.0.0", false)]
    [InlineData("v0.9.0", "1.0.0", false)]
    [InlineData("v1.0.10", "1.0.9", true)]
    [InlineData("kaputt", "1.0.0", false)]
    [InlineData(null, "1.0.0", false)]
    public void Compares_versions_numerically(string? tag, string current, bool newer)
    {
        Assert.Equal(newer, GitHubReleaseHint.IsNewer(tag, current));
    }

    [Fact]
    public async Task A_newer_release_is_found()
    {
        _http.On(HttpMethod.Get, GitHubReleaseHint.LatestReleaseApi, HttpStatusCode.OK, """{"tag_name":"v1.1.0","name":"v1.1.0"}""");
        var hint = new GitHubReleaseHint(new HttpClient(_http), new AppInfo("1.0.0"));

        Assert.False(hint.CanSelfUpdate);
        Assert.Equal("1.1.0", await hint.FindNewerAsync(_ct));
    }

    [Fact]
    public async Task A_refused_request_throws()
    {
        _http.On(HttpMethod.Get, GitHubReleaseHint.LatestReleaseApi, HttpStatusCode.Forbidden, """{"message":"rate limit"}""");
        var hint = new GitHubReleaseHint(new HttpClient(_http), new AppInfo("1.0.0"));

        await Assert.ThrowsAsync<HttpRequestException>(() => hint.FindNewerAsync(_ct));
    }
}
