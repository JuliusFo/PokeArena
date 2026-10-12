using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Core.Tests.Fakes;
using PokeArena.Core.Twitch;
using PokeArena.Core.Twitch.Auth;

namespace PokeArena.Core.Tests.Twitch;

public class TwitchAuthTests
{
    private const string Id = "https://id.twitch.tv/oauth2/";

    private readonly FakeHttpHandler _http = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));
    private readonly FakeSecretStore _secrets = new();
    private readonly TwitchAuthClient _auth;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public TwitchAuthTests()
    {
        _auth = new TwitchAuthClient(new HttpClient(_http), new TwitchOptions { ClientId = "client-1", Channel = "c" }, _time);
    }

    [Fact]
    public async Task Device_login_start_sends_client_id_and_scopes_and_reads_the_code()
    {
        _http.On(HttpMethod.Post, Id + "device", HttpStatusCode.OK, """
            {"device_code":"dev-1","expires_in":1800,"interval":5,"user_code":"ABCDEFGH","verification_uri":"https://www.twitch.tv/activate?public=true&device-code=ABCDEFGH"}
            """);

        var login = await _auth.StartDeviceLoginAsync(_ct);

        Assert.Equal("ABCDEFGH", login.UserCode);
        Assert.Equal("dev-1", login.DeviceCode);
        Assert.Equal(_time.GetUtcNow().AddMinutes(30), login.ExpiresAt);
        Assert.Equal(TimeSpan.FromSeconds(5), login.PollInterval);
        var body = _http.Requests.Single().Body;
        Assert.Contains("client_id=client-1", body);
        Assert.Contains("scopes=user%3Aread%3Achat+user%3Awrite%3Achat+user%3Abot", body);
    }

    [Theory]
    [InlineData("authorization_pending", typeof(DevicePollResult.Pending))]
    [InlineData("invalid device code", typeof(DevicePollResult.Expired))]
    public async Task Device_login_poll_maps_pending_and_expired(string message, Type expected)
    {
        _http.On(HttpMethod.Post, Id + "token", HttpStatusCode.BadRequest, $$"""{"status":400,"message":"{{message}}"}""");

        var result = await _auth.PollDeviceLoginAsync(Login(), _ct);

        Assert.IsType(expected, result);
    }

    [Fact]
    public async Task Device_login_poll_returns_tokens_once_confirmed()
    {
        _http.On(HttpMethod.Post, Id + "token", HttpStatusCode.OK, """
            {"access_token":"at-1","expires_in":14400,"refresh_token":"rt-1","scope":["user:read:chat"],"token_type":"bearer"}
            """);

        var result = await _auth.PollDeviceLoginAsync(Login(), _ct);

        var token = Assert.IsType<DevicePollResult.Authorized>(result).Token;
        Assert.Equal(new TwitchToken("at-1", "rt-1", _time.GetUtcNow().AddHours(4)), token);
        Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code", _http.Requests.Single().Body);
    }

    [Fact]
    public async Task Refresh_without_client_secret_and_rejection_requires_login()
    {
        _http.On(HttpMethod.Post, Id + "token", HttpStatusCode.BadRequest, """{"error":"Bad Request","status":400,"message":"Invalid refresh token"}""");

        await Assert.ThrowsAsync<TwitchAuthRequiredException>(() => _auth.RefreshAsync("rt-old", _ct));

        var body = _http.Requests.Single().Body;
        Assert.Contains("grant_type=refresh_token", body);
        Assert.Contains("refresh_token=rt-old", body);
        Assert.DoesNotContain("client_secret", body);
    }

    [Fact]
    public async Task Validate_returns_null_for_a_rejected_token()
    {
        _http.On(HttpMethod.Get, Id + "validate", HttpStatusCode.Unauthorized, """{"status":401,"message":"invalid access token"}""");

        Assert.Null(await _auth.ValidateAsync("at-1", _ct));
        Assert.Equal("OAuth at-1", _http.Requests.Single().Authorization);
    }

    [Fact]
    public async Task Provider_hands_out_a_fresh_token_without_refreshing()
    {
        var provider = Provider(new TwitchToken("at-1", "rt-1", _time.GetUtcNow().AddHours(1)));

        Assert.Equal("at-1", await provider.GetAccessTokenAsync(_ct));
        Assert.Empty(_http.Requests);
    }

    [Fact]
    public async Task Provider_refreshes_a_token_about_to_expire_and_stores_the_new_refresh_token()
    {
        var provider = Provider(new TwitchToken("at-1", "rt-1", _time.GetUtcNow().AddMinutes(2)));
        _http.On(HttpMethod.Post, Id + "token", HttpStatusCode.OK, """{"access_token":"at-2","expires_in":14400,"refresh_token":"rt-2"}""");

        Assert.Equal("at-2", await provider.GetAccessTokenAsync(_ct));

        var reloaded = Provider(stored: null);
        Assert.Equal("at-2", await reloaded.GetAccessTokenAsync(_ct));
        Assert.Contains("rt-2", _secrets.Get(TwitchTokenProvider.SecretName));
    }

    [Fact]
    public async Task Concurrent_refreshes_spend_the_single_use_refresh_token_only_once()
    {
        var provider = Provider(new TwitchToken("at-1", "rt-1", _time.GetUtcNow().AddHours(1)));
        _http.On(HttpMethod.Post, Id + "token", HttpStatusCode.OK, """{"access_token":"at-2","expires_in":14400,"refresh_token":"rt-2"}""");

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => provider.RefreshAsync("at-1", _ct)));

        Assert.All(results, r => Assert.Equal("at-2", r));
        Assert.Single(_http.RequestsTo(Id + "token"));
    }

    [Fact]
    public async Task Rejected_refresh_clears_the_stored_login()
    {
        var provider = Provider(new TwitchToken("at-1", "rt-1", _time.GetUtcNow()));
        _http.On(HttpMethod.Post, Id + "token", HttpStatusCode.BadRequest, """{"status":400,"message":"Invalid refresh token"}""");

        await Assert.ThrowsAsync<TwitchAuthRequiredException>(() => provider.GetAccessTokenAsync(_ct));

        Assert.False(provider.HasToken);
        Assert.Null(_secrets.Get(TwitchTokenProvider.SecretName));
    }

    [Fact]
    public async Task Validation_refreshes_once_when_the_access_token_is_rejected()
    {
        var provider = Provider(new TwitchToken("at-1", "rt-1", _time.GetUtcNow().AddHours(1)));
        _http.On(HttpMethod.Get, Id + "validate", HttpStatusCode.Unauthorized, """{"status":401,"message":"invalid access token"}""")
            .On(HttpMethod.Get, Id + "validate", HttpStatusCode.OK, ValidJson("user:read:chat", "user:write:chat", "user:bot"))
            .On(HttpMethod.Post, Id + "token", HttpStatusCode.OK, """{"access_token":"at-2","expires_in":14400,"refresh_token":"rt-2"}""");

        var validation = await provider.ValidateAsync(_ct);

        Assert.Equal("pokearenabot", validation.Login);
        Assert.Equal("OAuth at-2", _http.RequestsTo(Id + "validate").Last().Authorization);
    }

    [Fact]
    public async Task Validation_of_a_login_without_all_scopes_requires_a_new_login()
    {
        var provider = Provider(new TwitchToken("at-1", "rt-1", _time.GetUtcNow().AddHours(1)));
        _http.On(HttpMethod.Get, Id + "validate", HttpStatusCode.OK, ValidJson("user:read:chat"));

        var ex = await Assert.ThrowsAsync<TwitchAuthRequiredException>(() => provider.ValidateAsync(_ct));

        Assert.Contains("user:write:chat", ex.Message);
        Assert.False(provider.HasToken);
    }

    [Fact]
    public void Unreadable_stored_login_counts_as_signed_out()
    {
        _secrets.Set(TwitchTokenProvider.SecretName, "not json");

        Assert.False(Provider(stored: null).HasToken);
    }

    private TwitchTokenProvider Provider(TwitchToken? stored)
    {
        var provider = new TwitchTokenProvider(_auth, _secrets, _time, NullLogger<TwitchTokenProvider>.Instance);
        if (stored is not null)
        {
            provider.Save(stored);
        }

        return provider;
    }

    private DeviceAuthorization Login() =>
        new("dev-1", "ABCDEFGH", new Uri("https://www.twitch.tv/activate"), _time.GetUtcNow().AddMinutes(30), TimeSpan.FromSeconds(5));

    private static string ValidJson(params string[] scopes) =>
        $$"""{"client_id":"client-1","login":"pokearenabot","scopes":[{{string.Join(",", scopes.Select(s => $"\"{s}\""))}}],"user_id":"2001","expires_in":5000}""";
}
