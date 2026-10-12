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
}

/// <summary>Dry run ("Probelauf"): replies only go to the chat log, nothing reaches Twitch.</summary>
public sealed class DryRunMode
{
    private volatile bool _enabled;

    public event Action? Changed;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled != value)
            {
                _enabled = value;
                Changed?.Invoke();
            }
        }
    }
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
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly SendRateLimiter _limiter = new();

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
                _queue.Writer.TryWrite(part);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var text in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            var delay = _limiter.GetDelay(time.GetUtcNow());
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, time, stoppingToken);
            }

            await SendNowAsync(text, stoppingToken);
        }
    }

    private async Task SendNowAsync(string text, CancellationToken ct)
    {
        var target = targets.Target;
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
}
