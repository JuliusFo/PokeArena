using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PokeArena.Core.Chat;

namespace PokeArena.Core.Commands;

/// <summary>
/// The single consumer of <see cref="ChatInbox"/>: logs every message and runs the matching command, one message
/// at a time. Unknown commands are ignored silently — they may belong to another bot in the channel.
/// </summary>
public sealed class CommandProcessor(
    ChatInbox inbox,
    IEnumerable<IChatCommand> commands,
    IChatSender chat,
    ChatLog chatLog,
    ILogger<CommandProcessor> log) : BackgroundService
{
    private readonly Dictionary<string, IChatCommand> _commands = commands.ToDictionary(c => c.Name);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in inbox.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessAsync(message, stoppingToken);
        }
    }

    internal async Task ProcessAsync(ChatMessage message, CancellationToken ct)
    {
        chatLog.Add(ChatLogKind.Incoming, message.DisplayName, message.Text, message.IsSimulated);

        if (ParsedCommand.TryParse(message.Text) is not { } parsed
            || !_commands.TryGetValue(parsed.Name, out var command)
            || (command.Access == CommandAccess.Moderators && !message.IsModerator))
        {
            return;
        }

        try
        {
            await command.ExecuteAsync(new CommandContext(message, parsed.Arguments, chat), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            log.LogError(ex, "Command !{Command} from {UserId} failed", parsed.Name, message.UserId);
            chatLog.Add(ChatLogKind.Warning, null, $"Fehler bei !{parsed.Name} – Details stehen in der Logdatei.");
        }
    }
}
