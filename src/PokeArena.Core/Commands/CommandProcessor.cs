using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PokeArena.Core.Chat;
using PokeArena.Core.Game;
using PokeArena.Core.Twitch;

namespace PokeArena.Core.Commands;

/// <summary>
/// The single consumer of <see cref="ChatInbox"/>: logs every message and runs the matching command, one item
/// at a time, interleaved with <see cref="QueuedWork"/> such as a closing boss lobby. Unknown commands are ignored silently — they may belong to another bot in the channel. During the
/// dry run only simulated messages are processed; outside it only real ones, and only while the channel is live
/// (<see cref="StreamStatus"/>, spec §8).
/// </summary>
public sealed class CommandProcessor(
    ChatInbox inbox,
    IEnumerable<IChatCommand> commands,
    IChatSender chat,
    TrainerService trainers,
    DryRunMode dryRun,
    StreamStatus stream,
    ChatLog chatLog,
    TimeProvider time,
    ILogger<CommandProcessor> log) : BackgroundService
{
    /// <summary>One offline hint for the whole chat at most this often.</summary>
    public static readonly TimeSpan OfflineHintCooldown = TimeSpan.FromMinutes(5);

    internal const string OfflineHint = "PokeArena läuft nur, solange der Stream live ist. Bis zum nächsten Stream!";

    private readonly Dictionary<string, IChatCommand> _commands = commands.ToDictionary(c => c.Name);
    private DateTimeOffset? _lastOfflineHint;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in inbox.Reader.ReadAllAsync(stoppingToken))
        {
            switch (item)
            {
                case ChatMessage message:
                    await ProcessAsync(message, stoppingToken);
                    break;
                case QueuedWork work:
                    await RunAsync(work, stoppingToken);
                    break;
            }
        }
    }

    internal async Task RunAsync(QueuedWork work, CancellationToken ct)
    {
        try
        {
            await work.RunAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log.LogError(ex, "Queued work {Name} failed", work.Name);
            chatLog.Add(ChatLogKind.Warning, null, $"Fehler bei {work.Name} – Details stehen in der Logdatei.");
        }
    }

    internal async Task ProcessAsync(ChatMessage message, CancellationToken ct)
    {
        chatLog.Add(ChatLogKind.Incoming, message.DisplayName, message.Text, message.IsSimulated);

        // Real messages must not change data during the dry run, and a simulated message still queued when the dry
        // run ended must not reach the live database.
        if (message.IsSimulated != dryRun.Enabled)
        {
            return;
        }

        var parsed = ParsedCommand.TryParse(message.Text);
        if (!message.IsSimulated && !stream.AcceptsCommands)
        {
            var known = parsed is null ? null : _commands.GetValueOrDefault(parsed.Name);
            // Mod commands are administration, not play (e.g. deleting a trainer after the stream): they pass.
            if (known is not { Access: CommandAccess.Moderators })
            {
                if (known is not null)
                {
                    SendOfflineHint();
                }

                return;
            }
        }

        try
        {
            var registered = await trainers.ObserveAsync(message, ct);
            if (parsed is null || !_commands.TryGetValue(parsed.Name, out var command))
            {
                return;
            }

            switch (command.Access)
            {
                case CommandAccess.Moderators when !message.IsModerator:
                    return;
                case CommandAccess.Registered when !registered:
                    chat.Send($"@{message.DisplayName}, du bist noch nicht dabei. Melde dich an, zum Beispiel mit !registrieren Pikachu.");
                    return;
            }

            await command.ExecuteAsync(new CommandContext(message, parsed.Arguments, chat), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log.LogError(ex, "Processing \"{Text}\" from {UserId} failed", message.Text, message.UserId);
            var what = parsed is null ? "einer Chatnachricht" : $"!{parsed.Name}";
            chatLog.Add(ChatLogKind.Warning, null, $"Fehler bei {what} – Details stehen in der Logdatei.");
        }
    }

    private void SendOfflineHint()
    {
        var now = time.GetUtcNow();
        if (_lastOfflineHint is { } last && now - last < OfflineHintCooldown)
        {
            return;
        }

        _lastOfflineHint = now;
        chat.Send(OfflineHint);
    }
}
