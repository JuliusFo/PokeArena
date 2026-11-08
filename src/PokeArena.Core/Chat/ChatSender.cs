using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PokeArena.Core.Twitch.Helix;

namespace PokeArena.Core.Chat;

/// <summary>Sends bot messages to the chat. Never blocks: messages are queued and paced.</summary>
public interface IChatSender
{
    /// <summary>Queues <paramref name="text"/>; longer texts go out as several messages.</summary>
    void Send(string text);

    /// <summary>
    /// An error or hint for one viewer (unknown name, too few Pokémon, cooldown …): at most one per viewer, command
    /// and <see cref="ChatSender.HintInterval"/>, so a viewer spamming a command cannot fill the send queue and delay
    /// fight results, while a hint for another command still gets through (spec §8, review 1.0.3 A5). Not throttled
    /// in the dry run, where the streamer tests replies on purpose. Test doubles may simply send.
    /// </summary>
    /// <param name="command">The command the hint answers, without „!“.</param>
    void SendHint(string viewerId, string command, string text) => Send(text);
}

/// <summary>Where the bot writes: the channel and the bot account, known once connected.</summary>
public sealed record ChatTarget(string BroadcasterId, string BroadcasterLogin, string BotUserId, string BotLogin);

public interface IChatTargetSource
{
    /// <summary>The current target, or null while not connected.</summary>
    ChatTarget? Target { get; }
}

/// <summary>
/// Queue plus send loop: paces messages with <see cref="SendRateLimiter"/> and sends them through Helix, or only
/// logs them during a dry run. Failed or dropped messages are logged, not retried — a retry could post twice.
/// </summary>
public sealed class ChatSender(
    HelixClient helix,
    IChatTargetSource targets,
    DryRunMode dryRun,
    ChatLog chatLog,
    TimeProvider time,
    ILogger<ChatSender> log) : BackgroundService, IChatSender
{
    private readonly Channel<Queued> _queue = Channel.CreateUnbounded<Queued>(new UnboundedChannelOptions { SingleReader = true });
    private readonly SendRateLimiter _limiter = new();

    /// <summary>How long a message waits for a lost Twitch connection (reconnects back off up to 60 s).</summary>
    internal static readonly TimeSpan TargetWait = TimeSpan.FromSeconds(60);

    /// <summary>While the app closes: the host gives all services 5 s together.</summary>
    internal static readonly TimeSpan StopTargetWait = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan TargetPoll = TimeSpan.FromSeconds(1);

    /// <summary>UTC ticks of when <see cref="StopAsync"/> began; 0 while running. Ticks, so the send loop reads it atomically.</summary>
    private long _stoppingTicks;

    /// <summary>One error or hint reply per viewer at most this often (spec §8).</summary>
    public static readonly TimeSpan HintInterval = TimeSpan.FromSeconds(30);

    private readonly ReplyThrottle _hints = new(HintInterval, time);

    public void SendHint(string viewerId, string command, string text)
    {
        var allowed = dryRun.Enabled;
        if (!allowed)
        {
            lock (_hints)
            {
                allowed = _hints.Allows(viewerId + "\n" + command);
            }
        }

        if (allowed)
        {
            Send(text);
        }
    }

    public void Send(string text)
    {
        foreach (var part in ChatText.Split(text))
        {
            if (dryRun.Enabled)
            {
                chatLog.Add(ChatLogKind.Outgoing, targets.Target?.BotLogin ?? "Bot", part, dryRun: true);
            }
            else
            {
                _queue.Writer.TryWrite(new Queued(part, time.GetUtcNow()));
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            var delay = _limiter.GetDelay(time.GetUtcNow());
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, time, stoppingToken);
            }

            await SendNowAsync(message, stoppingToken);
        }
    }

    /// <summary>
    /// Sends what is still queued (e.g. the result of the last fight) before stopping, until the host's stop timeout.
    /// Register this service after the Twitch connection, so it stops first while the connection is still up.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _stoppingTicks, time.GetUtcNow().UtcTicks);
        _queue.Writer.TryComplete();
        if (ExecuteTask is { } running)
        {
            await running.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// During a reconnect there is no target for a few seconds; a fight result posted then would be lost. Wait for
    /// the connection instead — a message that was never sent cannot arrive twice.
    /// </summary>
    /// <remarks>
    /// Each message waits until <see cref="TargetWait"/> after it was queued, not after it reached the front: after a
    /// minute without Twitch (login refused, long outage) a backlog is dropped at once instead of one message per
    /// minute, nothing stale goes out much later, and a new message still gets its full minute. While stopping it
    /// waits at most <see cref="StopTargetWait"/>, so the Twitch connection still gets its share of the host's stop
    /// time (R1 second review).
    /// </remarks>
    private async Task<ChatTarget?> WaitForTargetAsync(Queued message, CancellationToken ct)
    {
        while (targets.Target is null && time.GetUtcNow() < Deadline(message))
        {
            await Task.Delay(TargetPoll, time, ct);
        }

        return targets.Target;
    }

    private DateTimeOffset Deadline(Queued message)
    {
        var deadline = message.QueuedAt + TargetWait;
        var stopping = Interlocked.Read(ref _stoppingTicks);
        return stopping == 0 ? deadline : new DateTimeOffset(Math.Min(deadline.UtcTicks, stopping + StopTargetWait.Ticks), TimeSpan.Zero);
    }

    private async Task SendNowAsync(Queued message, CancellationToken ct)
    {
        var text = message.Text;
        var target = await WaitForTargetAsync(message, ct);
        if (target is null)
        {
            chatLog.Add(ChatLogKind.Warning, null, $"Nicht mit Twitch verbunden – Nachricht nicht gesendet: {text}");
            return;
        }

        _limiter.Record(time.GetUtcNow());
        try
        {
            var result = await helix.SendChatMessageAsync(target.BroadcasterId, target.BotUserId, text, ct);
            if (result.IsSent)
            {
                chatLog.Add(ChatLogKind.Outgoing, target.BotLogin, text);
            }
            else
            {
                log.LogWarning("Twitch dropped a chat message: {Reason}", result.DropReason);
                chatLog.Add(ChatLogKind.Warning, null, $"Twitch hat die Nachricht nicht angenommen ({result.DropReason}): {text}");
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Sending a chat message failed");
            chatLog.Add(ChatLogKind.Warning, null, $"Senden fehlgeschlagen: {text}");
        }
    }

    private sealed record Queued(string Text, DateTimeOffset QueuedAt);
}
