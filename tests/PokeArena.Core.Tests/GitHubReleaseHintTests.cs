using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Core.Tests.Fakes;
using PokeArena.Core.Twitch;

namespace PokeArena.Core.Tests;

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
