using PokeArena.Core.Chat;

namespace PokeArena.Core.Commands;

/// <summary>
/// <c>!pokearena</c> (alias <c>!version</c>): answers that the bot is online and with which version (spec §8). A
/// short global cooldown, shared by both names, keeps a spamming viewer from filling the send queue. Runs on the
/// single command queue, so the cooldown field needs no lock.
/// </summary>
public sealed class StatusCommand(AppInfo app, TimeProvider time) : IChatCommand
{
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    private readonly ReplyThrottle _replies = new(Cooldown, time);

    public string Name => "pokearena";

    public IReadOnlyList<string> Aliases => ["version"];

    public CommandAccess Access => CommandAccess.Everyone;

    public Task ExecuteAsync(CommandContext context, CancellationToken ct)
    {
        // The dry run skips the cooldown: a streamer testing twice in a row would otherwise see no answer.
        if (!context.Message.IsSimulated && !_replies.Allows())
        {
            return Task.CompletedTask;
        }

        context.Chat.Send($"PokeArena-Bot ist online (Version {app.Version}).");
        return Task.CompletedTask;
    }
}
