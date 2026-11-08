using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PokeArena.Core.Chat;
using PokeArena.Core.Tests.Fakes;
using PokeArena.Core.Twitch;
using PokeArena.Core.Twitch.Auth;
using PokeArena.Core.Twitch.EventSub;
using PokeArena.Core.Twitch.Helix;

namespace PokeArena.Core.Tests.Twitch;

/// <summary>Login, connect, chat in and out — against fake HTTP and fake sockets.</summary>
public class TwitchConnectionTests
{
    private const string Id = "https://id.twitch.tv/oauth2/";
    private const string Helix = "https://api.twitch.tv/helix/";

    private readonly FakeHttpHandler _http = new();
    private readonly FakeEventSubSockets _sockets = new();
    private readonly FakeSecretStore _secrets = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));
    private readonly TwitchOptions _options = new() { ClientId = "client-1", Channel = "#ShiyoFurball" };
    private readonly ChatInbox _inbox = new();
    private readonly ChatLog _chatLog;
    private readonly StreamStatus _stream = new();
    private readonly TwitchTokenProvider _tokens;
    private readonly HelixClient _helix;
    private readonly TwitchConnection _connection;

    public TwitchConnectionTests()
    {
        var http = new HttpClient(_http);
        var auth = new TwitchAuthClient(http, _options, _time);
        _chatLog = new ChatLog(_time);
        _tokens = new TwitchTokenProvider(auth, _secrets, _time, NullLogger<TwitchTokenProvider>.Instance);
        _helix = new HelixClient(http, _options, _tokens);
        _connection = new TwitchConnection(
            _options,
            auth,
            _tokens,
            _helix,
            new EventSubSession(_sockets, _time, NullLogger<EventSubSession>.Instance),
            _inbox,
            _chatLog,
            _stream,
            _time,
            NullLogger<TwitchConnection>.Instance);
    }

    [Fact]
    public void Start_without_channel_reports_not_configured()
    {
        _options.Channel = "";

        _connection.Start();

        Assert.Equal(TwitchStatus.NotConfigured, _connection.State.Status);
    }

    [Fact]
    public void Start_without_login_waits_for_sign_in()
    {
        _connection.Start();

        Assert.Equal(TwitchStatus.SignedOut, _connection.State.Status);
    }

    [Fact]
    public async Task Device_login_then_connect_and_receive_chat()
    {
        _http.On(HttpMethod.Post, Id + "device", HttpStatusCode.OK, """
                {"device_code":"dev-1","expires_in":1800,"interval":5,"user_code":"ABCDEFGH","verification_uri":"https://www.twitch.tv/activate"}
                """)
            .On(HttpMethod.Post, Id + "token", HttpStatusCode.BadRequest, """{"status":400,"message":"authorization_pending"}""")
            .On(HttpMethod.Post, Id + "token", HttpStatusCode.OK, """{"access_token":"at-1","expires_in":14400,"refresh_token":"rt-1"}""");
        ArrangeConnect();

        await _connection.BeginLoginAsync();
        Assert.Equal(TwitchStatus.WaitingForLogin, _connection.State.Status);
        Assert.Equal("ABCDEFGH", _connection.State.Login?.UserCode);

        await AdvanceUntilAsync(() => _http.RequestsTo(Id + "token").Count() == 2, TimeSpan.FromSeconds(5));
        var socket = await _sockets.NextAsync();
        socket.Push(EventSubFrames.Welcome("session-7"));

        await Eventually.TrueAsync(() => _connection.State.Status == TwitchStatus.Connected);
        Assert.Equal("pokearenabot", _connection.State.BotLogin);
        Assert.Equal(new ChatTarget("42", "shiyofurball", "2001", "pokearenabot"), _connection.Target);
        var subscribe = _http.RequestsTo(Helix + "eventsub/subscriptions").First();
        Assert.Contains("\"type\":\"channel.chat.message\"", subscribe.Body);
        Assert.Contains("\"session_id\":\"session-7\"", subscribe.Body);
        Assert.Contains("\"broadcaster_user_id\":\"42\"", subscribe.Body);
        Assert.Equal("Bearer at-1", subscribe.Authorization);
        Assert.Equal("client-1", subscribe.ClientId);
        Assert.Contains("login=shiyofurball", _http.RequestsTo(Helix + "users").Single().Url);

        socket.Push(EventSubFrames.ChatMessage("!pokearena"));
        var message = await ReadInboxAsync();
        Assert.Equal("!pokearena", message.Text);

        await _connection.DisconnectAsync();
        Assert.Equal(TwitchStatus.Disconnected, _connection.State.Status);
        Assert.Null(_connection.Target);
        Assert.True(socket.Disposed);
    }

    [Fact]
    public async Task Lost_connection_is_retried_with_a_new_session()
    {
        StoreLogin();
        ArrangeConnect();

        _connection.Start();
        var first = await _sockets.NextAsync();
        first.Push(EventSubFrames.Welcome("session-1"));
        await Eventually.TrueAsync(() => _connection.State.Status == TwitchStatus.Connected);

        first.ServerClose("4006 network error");
        await Eventually.TrueAsync(() => _connection.State.Status == TwitchStatus.Reconnecting);
        Assert.Equal(TwitchProblem.ConnectionLost, _connection.State.Problem);
        Assert.Null(_connection.Target);

        await AdvanceUntilAsync(() => _sockets.ConnectedUrls.Count == 2, TwitchConnection.RetryDelay(1));
        var second = await _sockets.NextAsync();
        second.Push(EventSubFrames.Welcome("session-2"));

        await Eventually.TrueAsync(() => _connection.State.Status == TwitchStatus.Connected);
        Assert.Equal(6, _http.RequestsTo(Helix + "eventsub/subscriptions").Count());
        await _connection.DisconnectAsync();
    }

    [Fact]
    public async Task Rejected_login_signs_out_and_asks_for_a_new_login()
    {
        StoreLogin();
        _http.On(HttpMethod.Get, Id + "validate", HttpStatusCode.Unauthorized, """{"status":401,"message":"invalid access token"}""")
            .On(HttpMethod.Post, Id + "token", HttpStatusCode.BadRequest, """{"status":400,"message":"Invalid refresh token"}""");

        _connection.Start();

        await Eventually.TrueAsync(() => _connection.State.Status == TwitchStatus.SignedOut);
        Assert.Equal(TwitchProblem.SessionExpired, _connection.State.Problem);
        Assert.False(_tokens.HasToken);
    }

    [Fact]
    public async Task Unknown_channel_stops_without_retrying()
    {
        StoreLogin();
        _http.On(HttpMethod.Get, Id + "validate", HttpStatusCode.OK, ValidJson)
            .On(HttpMethod.Get, Helix + "users", HttpStatusCode.OK, """{"data":[]}""");

        _connection.Start();

        await Eventually.TrueAsync(() => _connection.State.Status == TwitchStatus.Failed);
        Assert.Equal(TwitchProblem.ChannelNotFound, _connection.State.Problem);
    }

    [Fact]
    public async Task Sender_posts_through_helix_when_connected_and_only_logs_in_the_dry_run()
    {
        StoreLogin();
        ArrangeConnect();
        _http.On(HttpMethod.Post, Helix + "chat/messages", HttpStatusCode.OK, """{"data":[{"message_id":"x","is_sent":true,"drop_reason":null}]}""");
        var dryRun = new DryRunMode();
        var sender = new ChatSender(_helix, _connection, dryRun, _chatLog, _time, NullLogger<ChatSender>.Instance);
        await sender.StartAsync(TestContext.Current.CancellationToken);
        _connection.Start();
        (await _sockets.NextAsync()).Push(EventSubFrames.Welcome());
        await Eventually.TrueAsync(() => _connection.State.Status == TwitchStatus.Connected);

        sender.Send("Hallo Chat");
        await Eventually.TrueAsync(() => _http.RequestsTo(Helix + "chat/messages").Any());
        dryRun.Enabled = true;
        sender.Send("Nur im Log");

        var sent = _http.RequestsTo(Helix + "chat/messages").Single();
        Assert.Contains("\"broadcaster_id\":\"42\"", sent.Body);
        Assert.Contains("\"sender_id\":\"2001\"", sent.Body);
        Assert.Contains("\"message\":\"Hallo Chat\"", sent.Body);
        await Eventually.TrueAsync(() => _chatLog.Snapshot().Any(e => e.Kind == ChatLogKind.Outgoing && !e.DryRun));
        Assert.Contains(_chatLog.Snapshot(), e => e is { Kind: ChatLogKind.Outgoing, DryRun: true, Text: "Nur im Log" });

        await sender.StopAsync(TestContext.Current.CancellationToken);
        await _connection.DisconnectAsync();
    }

    [Fact]
    public async Task Connect_reads_the_live_state_and_follows_stream_events()
    {
        StoreLogin();
        _http.On(HttpMethod.Get, Helix + "streams", HttpStatusCode.OK, """{"data":[{"id":"s-1","user_id":"42","type":"live"}]}""");
        ArrangeConnect();

        _connection.Start();
        var socket = await _sockets.NextAsync();
        socket.Push(EventSubFrames.Welcome());

        await Eventually.TrueAsync(() => _stream.IsLive);
        var subscriptions = _http.RequestsTo(Helix + "eventsub/subscriptions").Select(r => r.Body).ToList();
        Assert.Contains(subscriptions, b => b.Contains("\"type\":\"stream.online\"") && b.Contains("\"broadcaster_user_id\":\"42\""));
        Assert.Contains(subscriptions, b => b.Contains("\"type\":\"stream.offline\"") && b.Contains("\"broadcaster_user_id\":\"42\""));
        Assert.Contains("user_id=42", _http.RequestsTo(Helix + "streams").Single().Url);

        // Asked only after both subscriptions exist, or a change in between would get lost (Core pitfall).
        var order = _http.Requests.Select(r => r.Url).ToList();
        Assert.True(
            order.FindIndex(u => u.StartsWith(Helix + "streams", StringComparison.Ordinal))
                > order.FindLastIndex(u => u.StartsWith(Helix + "eventsub/subscriptions", StringComparison.Ordinal)));
        Assert.Contains(_chatLog.Snapshot(), e => e.Text == "Der Stream ist live – PokeArena nimmt Befehle an.");

        socket.Push(EventSubFrames.Stream("stream.offline"));
        await Eventually.TrueAsync(() => !_stream.IsLive);
        socket.Push(EventSubFrames.Stream("stream.online"));
        await Eventually.TrueAsync(() => _stream.IsLive);

        await _connection.DisconnectAsync();
    }

    [Fact]
    public async Task Hourly_check_catches_a_missed_stream_event()
    {
        StoreLogin();
        ArrangeConnect();
        _http.On(HttpMethod.Get, Helix + "streams", HttpStatusCode.OK, """{"data":[{"id":"s-1","user_id":"42","type":"live"}]}""");

        _connection.Start();
        (await _sockets.NextAsync()).Push(EventSubFrames.Welcome(keepaliveSeconds: 7200));
        await Eventually.TrueAsync(() => _connection.State.Status == TwitchStatus.Connected);
        await Eventually.TrueAsync(() => _chatLog.Snapshot().Any(e => e.Text.StartsWith("Der Stream ist offline", StringComparison.Ordinal)));
        Assert.False(_stream.IsLive);

        _time.Advance(TwitchConnection.ValidationInterval);

        await Eventually.TrueAsync(() => _stream.IsLive);
        await _connection.DisconnectAsync();
    }

    [Fact]
    public async Task A_login_with_the_channel_account_stops_with_a_hint()
    {
        StoreLogin();
        _http.On(HttpMethod.Get, Id + "validate", HttpStatusCode.OK,
                """{"client_id":"client-1","login":"shiyofurball","scopes":["user:read:chat","user:write:chat","user:bot"],"user_id":"42","expires_in":14000}""")
            .On(HttpMethod.Get, Helix + "users", HttpStatusCode.OK, """{"data":[{"id":"42","login":"shiyofurball","display_name":"ShiyoFurball"}]}""");

        _connection.Start();

        await Eventually.TrueAsync(() => _connection.State.Status == TwitchStatus.Failed);
        Assert.Equal(TwitchProblem.WrongAccount, _connection.State.Problem);
        Assert.Empty(_sockets.ConnectedUrls);
        Assert.True(_tokens.HasToken);
    }

    [Fact]
    public async Task A_revoked_authorization_signs_the_bot_out()
    {
        StoreLogin();
        ArrangeConnect();
        _connection.Start();
        var socket = await _sockets.NextAsync();
        socket.Push(EventSubFrames.Welcome());
        await Eventually.TrueAsync(() => _connection.State.Status == TwitchStatus.Connected);

        socket.Push(EventSubFrames.Revocation("authorization_revoked"));

        await Eventually.TrueAsync(() => _connection.State.Status == TwitchStatus.SignedOut);
        Assert.Equal(TwitchProblem.SessionExpired, _connection.State.Problem);
        Assert.False(_tokens.HasToken);
    }

    [Fact]
    public async Task A_refused_subscription_is_retried_and_only_403_stops()
    {
        StoreLogin();
        _http.On(HttpMethod.Post, Helix + "eventsub/subscriptions", HttpStatusCode.BadRequest, """{"status":400,"message":"session does not exist"}""");
        ArrangeConnect();

        _connection.Start();
        (await _sockets.NextAsync()).Push(EventSubFrames.Welcome("session-1"));

        await Eventually.TrueAsync(() => _connection.State.Status == TwitchStatus.Reconnecting);
        await AdvanceUntilAsync(() => _sockets.ConnectedUrls.Count == 2, TwitchConnection.RetryDelay(1));
        (await _sockets.NextAsync()).Push(EventSubFrames.Welcome("session-2"));
        await Eventually.TrueAsync(() => _connection.State.Status == TwitchStatus.Connected);
        await _connection.DisconnectAsync();
    }

    [Fact]
    public async Task Forbidden_subscription_stops_with_missing_permission()
    {
        StoreLogin();
        _http.On(HttpMethod.Post, Helix + "eventsub/subscriptions", HttpStatusCode.Forbidden, """{"status":403,"message":"forbidden"}""");
        ArrangeConnect();

        _connection.Start();
        (await _sockets.NextAsync()).Push(EventSubFrames.Welcome());

        await Eventually.TrueAsync(() => _connection.State.Status == TwitchStatus.Failed);
        Assert.Equal(TwitchProblem.MissingPermission, _connection.State.Problem);
    }

    [Fact]
    public async Task A_stream_that_went_live_just_before_the_start_is_found_by_a_recheck()
    {
        StoreLogin();
        ArrangeConnect();
        _http.On(HttpMethod.Get, Helix + "streams", HttpStatusCode.OK, """{"data":[{"id":"s-1","user_id":"42","type":"live"}]}""");

        _connection.Start();
        (await _sockets.NextAsync()).Push(EventSubFrames.Welcome(keepaliveSeconds: 7200));
        await Eventually.TrueAsync(() => _http.RequestsTo(Helix + "streams").Count() == 1);
        Assert.False(_stream.IsLive);

        _time.Advance(TwitchConnection.StartRecheckInterval);

        await Eventually.TrueAsync(() => _stream.IsLive);
        await _connection.DisconnectAsync();
    }

    [Fact]
    public async Task A_lagging_check_does_not_override_a_recent_stream_event()
    {
        StoreLogin();
        ArrangeConnect();

        _connection.Start();
        var socket = await _sockets.NextAsync();
        socket.Push(EventSubFrames.Welcome(keepaliveSeconds: 7200));
        await Eventually.TrueAsync(() => _http.RequestsTo(Helix + "streams").Count() == 1);
        socket.Push(EventSubFrames.Stream("stream.online"));
        await Eventually.TrueAsync(() => _stream.IsLive);
        socket.Push(EventSubFrames.Stream("stream.offline"));
        await Eventually.TrueAsync(() => !_stream.IsLive);

        // Helix still says live minutes after the offline event: the event wins. Had the first recheck set the
        // channel live, the rechecks would stop and there would be no third request.
        _http.On(HttpMethod.Get, Helix + "streams", HttpStatusCode.OK, """{"data":[{"id":"s-1","user_id":"42","type":"live"}]}""");
        _time.Advance(TwitchConnection.StartRecheckInterval);
        await Eventually.TrueAsync(() => _http.RequestsTo(Helix + "streams").Count() == 2);
        _time.Advance(TwitchConnection.StartRecheckInterval);
        await Eventually.TrueAsync(() => _http.RequestsTo(Helix + "streams").Count() == 3);

        Assert.False(_stream.IsLive);
        await _connection.DisconnectAsync();
    }

    [Fact]
    public async Task A_message_waits_for_a_reconnect_and_queued_messages_are_sent_before_stopping()
    {
        StoreLogin();
        ArrangeConnect();
        _http.On(HttpMethod.Post, Helix + "chat/messages", HttpStatusCode.OK, """{"data":[{"message_id":"x","is_sent":true,"drop_reason":null}]}""");
        var sender = new ChatSender(_helix, _connection, new DryRunMode(), _chatLog, _time, NullLogger<ChatSender>.Instance);
        await sender.StartAsync(TestContext.Current.CancellationToken);
        _connection.Start();
        var first = await _sockets.NextAsync();
        first.Push(EventSubFrames.Welcome("session-1"));
        await Eventually.TrueAsync(() => _connection.State.Status == TwitchStatus.Connected);
        first.ServerClose("4006 network error");
        await Eventually.TrueAsync(() => _connection.Target is null);

        sender.Send("Der Boss wurde besiegt!");
        var stopping = sender.StopAsync(TestContext.Current.CancellationToken);
        await AdvanceUntilAsync(() => _sockets.ConnectedUrls.Count == 2, TwitchConnection.RetryDelay(1));
        (await _sockets.NextAsync()).Push(EventSubFrames.Welcome("session-2"));
        await AdvanceUntilAsync(() => stopping.IsCompleted, TimeSpan.FromSeconds(1));

        Assert.Contains("Der Boss wurde besiegt!", _http.RequestsTo(Helix + "chat/messages").Single().Body);
        await _connection.DisconnectAsync();
    }

    [Fact]
    public async Task The_sender_splits_long_texts_and_keeps_one_second_between_messages()
    {
        StoreLogin();
        ArrangeConnect();
        _http.On(HttpMethod.Post, Helix + "chat/messages", HttpStatusCode.OK, """{"data":[{"message_id":"x","is_sent":true,"drop_reason":null}]}""");
        var sender = new ChatSender(_helix, _connection, new DryRunMode(), _chatLog, _time, NullLogger<ChatSender>.Instance);
        await sender.StartAsync(TestContext.Current.CancellationToken);
        _connection.Start();
        (await _sockets.NextAsync()).Push(EventSubFrames.Welcome());
        await Eventually.TrueAsync(() => _connection.State.Status == TwitchStatus.Connected);

        sender.Send(string.Join(' ', Enumerable.Repeat("Pikachu", 75)));
        await Eventually.TrueAsync(() => _http.RequestsTo(Helix + "chat/messages").Any());

        // The second part waits for the limiter (1 per second), driven by the fake clock.
        Assert.Single(_http.RequestsTo(Helix + "chat/messages"));
        _time.Advance(TimeSpan.FromSeconds(1));
        await Eventually.TrueAsync(() => _http.RequestsTo(Helix + "chat/messages").Count() == 2);
        Assert.All(_http.RequestsTo(Helix + "chat/messages"), r => Assert.True(r.Body.Length < ChatText.MaxLength + 200));

        await sender.StopAsync(TestContext.Current.CancellationToken);
        await _connection.DisconnectAsync();
    }

    [Fact]
    public async Task Hints_go_out_once_per_viewer_command_and_interval_except_in_the_dry_run()
    {
        StoreLogin();
        ArrangeConnect();
        _http.On(HttpMethod.Post, Helix + "chat/messages", HttpStatusCode.OK, """{"data":[{"message_id":"x","is_sent":true,"drop_reason":null}]}""");
        var dryRun = new DryRunMode();
        var sender = new ChatSender(_helix, _connection, dryRun, _chatLog, _time, NullLogger<ChatSender>.Instance);
        await sender.StartAsync(TestContext.Current.CancellationToken);
        _connection.Start();
        (await _sockets.NextAsync()).Push(EventSubFrames.Welcome(keepaliveSeconds: 7200));
        await Eventually.TrueAsync(() => _connection.State.Status == TwitchStatus.Connected);

        sender.SendHint("1", "fight", "erster");
        sender.SendHint("1", "fight", "gedrosselt");
        sender.SendHint("1", "registrieren", "anderer Befehl");
        sender.SendHint("2", "fight", "anderer Zuschauer");
        _time.Advance(ChatSender.HintInterval);
        sender.SendHint("1", "fight", "wieder erlaubt");
        await AdvanceUntilAsync(() => _http.RequestsTo(Helix + "chat/messages").Count() == 4, TimeSpan.FromSeconds(1));

        var posted = _http.RequestsTo(Helix + "chat/messages").Select(r => r.Body).ToList();
        Assert.All(["erster", "anderer Befehl", "anderer Zuschauer", "wieder erlaubt"], text => Assert.Contains(posted, b => b.Contains(text, StringComparison.Ordinal)));
        Assert.DoesNotContain(posted, b => b.Contains("gedrosselt", StringComparison.Ordinal));

        // The streamer tests replies on purpose: the dry run is never throttled.
        dryRun.Enabled = true;
        sender.SendHint("probelauf:a", "fight", "Probe 1");
        sender.SendHint("probelauf:a", "fight", "Probe 2");
        Assert.Equal(2, _chatLog.Snapshot().Count(e => e is { Kind: ChatLogKind.Outgoing, DryRun: true }));

        await sender.StopAsync(TestContext.Current.CancellationToken);
        await _connection.DisconnectAsync();
    }

    private const string ValidJson =
        """{"client_id":"client-1","login":"pokearenabot","scopes":["user:read:chat","user:write:chat","user:bot"],"user_id":"2001","expires_in":14000}""";

    private void StoreLogin() => _tokens.Save(new TwitchToken("at-1", "rt-1", _time.GetUtcNow().AddHours(4)));

    private void ArrangeConnect() =>
        _http.On(HttpMethod.Get, Id + "validate", HttpStatusCode.OK, ValidJson)
            .On(HttpMethod.Get, Helix + "users", HttpStatusCode.OK, """{"data":[{"id":"42","login":"shiyofurball","display_name":"ShiyoFurball"}]}""")
            .On(HttpMethod.Post, Helix + "eventsub/subscriptions", HttpStatusCode.Accepted, """{"data":[{"id":"sub-1","status":"enabled"}]}""")
            .On(HttpMethod.Get, Helix + "streams", HttpStatusCode.OK, """{"data":[]}""");

    private async Task<ChatMessage> ReadInboxAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return Assert.IsType<ChatMessage>(await _inbox.Reader.ReadAsync(timeout.Token));
    }

    /// <summary>Advances fake time in steps until <paramref name="condition"/> holds (for polling loops).</summary>
    private async Task AdvanceUntilAsync(Func<bool> condition, TimeSpan step)
    {
        for (var i = 0; i < 50 && !condition(); i++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
            _time.Advance(step);
        }

        await Eventually.TrueAsync(condition);
    }
}
