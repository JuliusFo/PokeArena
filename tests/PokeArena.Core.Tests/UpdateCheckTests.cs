using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PokeArena.Core.Tests.Fakes;

namespace PokeArena.Core.Tests;

public class UpdateCheckTests
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
        Assert.Equal(newer, UpdateCheck.IsNewer(tag, current));
    }

    [Fact]
    public async Task A_newer_release_is_reported()
    {
        _http.On(HttpMethod.Get, UpdateCheck.LatestReleaseApi, HttpStatusCode.OK, """{"tag_name":"v1.1.0","name":"v1.1.0"}""");
        var check = new UpdateCheck(new HttpClient(_http), new AppInfo("1.0.0"), NullLogger<UpdateCheck>.Instance);
        var raised = false;
        check.Changed += () => raised = true;

        await check.StartAsync(_ct);
        await check.ExecuteTask!;

        Assert.Equal("1.1.0", check.NewerVersion);
        Assert.True(raised);
    }

    [Fact]
    public async Task A_failed_check_shows_no_hint()
    {
        _http.On(HttpMethod.Get, UpdateCheck.LatestReleaseApi, HttpStatusCode.Forbidden, """{"message":"rate limit"}""");
        var check = new UpdateCheck(new HttpClient(_http), new AppInfo("1.0.0"), NullLogger<UpdateCheck>.Instance);

        await check.StartAsync(_ct);
        await check.ExecuteTask!;

        Assert.Null(check.NewerVersion);
    }
}
