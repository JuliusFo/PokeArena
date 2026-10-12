namespace PokeArena.Core.Commands;

/// <summary>
/// <c>!pokearena</c>: answers that the bot is online (spec §8). A short global cooldown keeps a spamming viewer
/// from filling the send queue. Runs on the single command queue, so the cooldown field needs no lock.
/// </summary>
public sealed class StatusCommand(AppInfo app, TimeProvider time) : IChatCommand
{
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    private DateTimeOffset? _lastReply;

    public string Name => "pokearena";

    public CommandAccess Access => CommandAccess.Everyone;

    public Task ExecuteAsync(CommandContext context, CancellationToken ct)
    {
        // The dry run skips the cooldown: a streamer testing twice in a row would otherwise see no answer.
        if (!context.Message.IsSimulated)
        {
            var now = time.GetUtcNow();
            if (_lastReply is { } last && now - last < Cooldown)
            {
                return Task.CompletedTask;
            }

            _lastReply = now;
        }

        context.Chat.Send($"PokeArena-Bot ist online (Version {app.Version}).");
        return Task.CompletedTask;
    }
}
