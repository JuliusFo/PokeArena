using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PokeArena.Core.Chat;
using PokeArena.Core.Game;

namespace PokeArena.Core.Commands;

/// <summary>
/// The single consumer of <see cref="ChatInbox"/>: logs every message and runs the matching command, one item
/// at a time, interleaved with <see cref="QueuedWork"/> such as a closing boss lobby. Unknown commands are ignored silently — they may belong to another bot in the channel. During the
/// dry run only simulated messages are processed; outside it only real ones.
/// </summary>
public sealed class CommandProcessor(
    ChatInbox inbox,
    IEnumerable<IChatCommand> commands,
    IChatSender chat,
    TrainerService trainers,
    DryRunMode dryRun,
    ChatLog chatLog,
    ILogger<CommandProcessor> log) : BackgroundService
{
    private readonly Dictionary<string, IChatCommand> _commands = commands.ToDictionary(c => c.Name);

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
                    chat.Send($"@{message.DisplayName}, du bist noch nicht dabei. Melde dich mit !registrieren <Starter> an.");
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
}
