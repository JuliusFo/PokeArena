using System.Net;
using Microsoft.Extensions.Logging;
using PokeArena.Core.Chat;
using PokeArena.Core.Twitch.Auth;
using PokeArena.Core.Twitch.EventSub;
using PokeArena.Core.Twitch.Helix;

namespace PokeArena.Core.Twitch;

/// <summary>
/// Ties login, chat subscription and reconnects together. While running it validates the token hourly (Twitch
/// requires it), feeds chat messages into the <see cref="ChatInbox"/> and provides the <see cref="ChatTarget"/>
/// for sending. Lost connections are retried with growing pauses; only problems a retry cannot fix stop it.
/// </summary>
public sealed class TwitchConnection(
    TwitchOptions options,
    TwitchAuthClient auth,
    TwitchTokenProvider tokens,
    HelixClient helix,
    EventSubSession eventSub,
    ChatInbox inbox,
    ChatLog chatLog,
    TimeProvider time,
    ILogger<TwitchConnection> log) : ITwitchConnection, IChatTargetSource
{
    internal static readonly TimeSpan ValidationInterval = TimeSpan.FromHours(1);

    private readonly Lock _lock = new();
    private TwitchConnectionState _state = new(TwitchStatus.Disconnected);
    private CancellationTokenSource? _loginCts;
    private CancellationTokenSource? _runCts;
    private Task _runTask = Task.CompletedTask;
    private volatile ChatTarget? _target;

    public event Action? StateChanged;

    public TwitchConnectionState State => _state;

    public string Channel => NormalizeChannel(options.Channel);

    public ChatTarget? Target => _target;

    /// <summary>App start: connects right away if a login is stored, so the streamer does not have to.</summary>
    public void Start()
    {
        if (!options.IsConfigured)
        {
            SetState(new TwitchConnectionState(TwitchStatus.NotConfigured));
        }
        else if (!tokens.HasToken)
        {
            SetState(new TwitchConnectionState(TwitchStatus.SignedOut));
        }
        else
        {
            Connect();
        }
    }

    public async Task BeginLoginAsync()
    {
        if (!options.IsConfigured)
        {
            return;
        }

        CancelLogin();
        var cts = new CancellationTokenSource();
        lock (_lock)
        {
            _loginCts = cts;
        }

        try
        {
            var login = await auth.StartDeviceLoginAsync(cts.Token);
            SetState(new TwitchConnectionState(TwitchStatus.WaitingForLogin, Login: login));
            _ = PollLoginAsync(login, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Cancelled by the user.
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Could not start the Twitch device login");
            SetState(new TwitchConnectionState(TwitchStatus.SignedOut, TwitchProblem.LoginFailed));
        }
    }

    public void CancelLogin()
    {
        CancellationTokenSource? cts;
        lock (_lock)
        {
            cts = _loginCts;
            _loginCts = null;
        }

        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        if (_state.Status == TwitchStatus.WaitingForLogin)
        {
            SetState(new TwitchConnectionState(TwitchStatus.SignedOut));
        }
    }

    public void Connect()
    {
        lock (_lock)
        {
            if (!_runTask.IsCompleted || !options.IsConfigured)
            {
                return;
            }

            _runCts = new CancellationTokenSource();
            _runTask = RunAsync(_runCts.Token);
        }
    }

    public async Task DisconnectAsync()
    {
        CancellationTokenSource? cts;
        Task task;
        lock (_lock)
        {
            cts = _runCts;
            task = _runTask;
            _runCts = null;
        }

        if (cts is not null)
        {
            await cts.CancelAsync();
            await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            cts.Dispose();
            chatLog.Add(ChatLogKind.Info, null, "Verbindung zu Twitch getrennt.");
        }

        SetState(new TwitchConnectionState(
            !options.IsConfigured ? TwitchStatus.NotConfigured
            : tokens.HasToken ? TwitchStatus.Disconnected
            : TwitchStatus.SignedOut));
    }

    public async Task SignOutAsync()
    {
        CancelLogin();
        await DisconnectAsync();
        tokens.Clear();
        SetState(new TwitchConnectionState(options.IsConfigured ? TwitchStatus.SignedOut : TwitchStatus.NotConfigured));
        log.LogInformation("Twitch login removed");
    }

    private async Task PollLoginAsync(DeviceAuthorization login, CancellationToken ct)
    {
        try
        {
            while (time.GetUtcNow() < login.ExpiresAt)
            {
                await Task.Delay(login.PollInterval, time, ct);
                DevicePollResult result;
                try
                {
                    result = await auth.PollDeviceLoginAsync(login, ct);
                }
                catch (HttpRequestException ex)
                {
                    log.LogWarning(ex, "Polling the Twitch device login failed, trying again");
                    continue;
                }

                switch (result)
                {
                    case DevicePollResult.Authorized authorized:
                        tokens.Save(authorized.Token);
                        log.LogInformation("Twitch device login completed");
                        Connect();
                        return;
                    case DevicePollResult.Expired:
                        SetState(new TwitchConnectionState(TwitchStatus.SignedOut, TwitchProblem.LoginExpired));
                        return;
                }
            }

            SetState(new TwitchConnectionState(TwitchStatus.SignedOut, TwitchProblem.LoginExpired));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Cancelled by the user.
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Twitch device login failed");
            SetState(new TwitchConnectionState(TwitchStatus.SignedOut, TwitchProblem.LoginFailed));
        }
        finally
        {
            lock (_lock)
            {
                if (_loginCts?.Token == ct)
                {
                    _loginCts = null;
                }
            }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        await Task.Yield(); // Leave the caller's lock before the first await does real work.
        var failures = 0;
        while (!ct.IsCancellationRequested)
        {
            SetState(new TwitchConnectionState(TwitchStatus.Connecting, BotLogin: _state.BotLogin));
            try
            {
                await RunSessionAsync(() => failures = 0, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (TwitchAuthRequiredException ex)
            {
                log.LogWarning("Twitch login no longer valid: {Reason}", ex.Message);
                tokens.Clear();
                chatLog.Add(ChatLogKind.Warning, null, "Die Twitch-Anmeldung ist abgelaufen. Bitte neu anmelden.");
                SetState(new TwitchConnectionState(TwitchStatus.SignedOut, TwitchProblem.SessionExpired));
                return;
            }
            catch (StopException ex)
            {
                log.LogWarning(ex, "Twitch connection stopped: {Problem}", ex.Problem);
                SetState(new TwitchConnectionState(TwitchStatus.Failed, ex.Problem));
                return;
            }
            catch (TwitchApiException ex) when (!ex.IsTransient)
            {
                log.LogWarning(ex, "Twitch refused the connection");
                SetState(new TwitchConnectionState(
                    TwitchStatus.Failed,
                    ex.Status == HttpStatusCode.Forbidden ? TwitchProblem.MissingPermission : TwitchProblem.ConnectionLost));
                return;
            }
            catch (Exception ex)
            {
                // Network trouble, Twitch hiccups, closed sockets — and anything unexpected: keep the bot alive.
                failures++;
                var delay = RetryDelay(failures);
                log.LogWarning(ex, "Twitch connection lost (attempt {Attempt}), retrying in {Delay}", failures, delay);
                chatLog.Add(ChatLogKind.Warning, null, $"Verbindung zu Twitch unterbrochen – neuer Versuch in {delay.TotalSeconds:0} Sekunden.");
                SetState(new TwitchConnectionState(
                    TwitchStatus.Reconnecting,
                    TwitchProblem.ConnectionLost,
                    _state.BotLogin,
                    RetryAt: time.GetUtcNow() + delay));
                try
                {
                    await Task.Delay(delay, time, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task RunSessionAsync(Action onSessionStarted, CancellationToken ct)
    {
        var bot = await tokens.ValidateAsync(ct);
        var channel = await helix.GetUserByLoginAsync(Channel, ct)
            ?? throw new StopException(TwitchProblem.ChannelNotFound, $"Twitch channel '{Channel}' not found");
        var target = new ChatTarget(channel.Id, channel.Login, bot.UserId, bot.Login);
        _target = target;
        try
        {
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var handler = new Handler(helix, inbox, target, () =>
            {
                onSessionStarted();
                OnConnected(target);
            });
            var session = eventSub.RunAsync(handler, stop.Token);
            var validation = ValidatePeriodicallyAsync(stop.Token);
            var first = await Task.WhenAny(session, validation);
            try
            {
                await first;
            }
            finally
            {
                await stop.CancelAsync();
                await Task.WhenAll(session, validation).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
        finally
        {
            _target = null;
        }
    }

    /// <summary>Hourly token check. Network hiccups are skipped; a rejected login ends the session.</summary>
    private async Task ValidatePeriodicallyAsync(CancellationToken ct)
    {
        while (true)
        {
            await Task.Delay(ValidationInterval, time, ct);
            try
            {
                await tokens.ValidateAsync(ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TwitchApiException { IsTransient: true })
            {
                log.LogWarning(ex, "Hourly Twitch token validation failed, trying again later");
            }
        }
    }

    internal static TimeSpan RetryDelay(int failures) => failures switch
    {
        <= 1 => TimeSpan.FromSeconds(2),
        2 => TimeSpan.FromSeconds(5),
        3 => TimeSpan.FromSeconds(15),
        4 => TimeSpan.FromSeconds(30),
        _ => TimeSpan.FromSeconds(60),
    };

    private static string NormalizeChannel(string channel) => channel.Trim().TrimStart('#').ToLowerInvariant();

    private void SetState(TwitchConnectionState state)
    {
        _state = state;
        StateChanged?.Invoke();
    }

    private void OnConnected(ChatTarget target)
    {
        SetState(new TwitchConnectionState(TwitchStatus.Connected, BotLogin: target.BotLogin));
        chatLog.Add(ChatLogKind.Info, null, $"Verbunden mit dem Chat von {target.BroadcasterLogin} als {target.BotLogin}.");
    }

    /// <summary>A problem that retrying cannot fix.</summary>
    private sealed class StopException(TwitchProblem problem, string message) : Exception(message)
    {
        public TwitchProblem Problem { get; } = problem;
    }

    private sealed class Handler(HelixClient helix, ChatInbox inbox, ChatTarget target, Action onSubscribed) : IEventSubHandler
    {
        public async Task OnSessionStartedAsync(string sessionId, CancellationToken ct)
        {
            await helix.SubscribeToChatMessagesAsync(sessionId, target.BroadcasterId, target.BotUserId, ct);
            onSubscribed();
        }

        public Task OnNotificationAsync(EventSubMessage.Notification notification, CancellationToken ct)
        {
            if (notification.SubscriptionType == ChatMessageEvent.SubscriptionType
                && ChatMessageEvent.Read(notification.Event, target) is { } message)
            {
                inbox.Post(message);
            }

            return Task.CompletedTask;
        }

        public Task OnRevocationAsync(EventSubMessage.Revocation revocation, CancellationToken ct) =>
            throw (revocation.Status == "authorization_revoked"
                ? new TwitchAuthRequiredException("The bot account revoked the app's access")
                : new StopException(TwitchProblem.SubscriptionRevoked, $"Subscription revoked: {revocation.Status}"));
    }
}
