using PokeArena.Core.Chat;
using PokeArena.Core.Publishing;

namespace PokeArena.Core.Commands;

/// <summary><c>!arena</c>: posts the website link (spec §8). Same short global cooldown as <c>!pokearena</c>.</summary>
public sealed class ArenaCommand(SiteLinks links, TimeProvider time) : IChatCommand
{
    private readonly ReplyThrottle _replies = new(StatusCommand.Cooldown, time);

    public string Name => "arena";

    public CommandAccess Access => CommandAccess.Everyone;

    public Task ExecuteAsync(CommandContext context, CancellationToken ct)
    {
        if (!context.Message.IsSimulated && !_replies.Allows())
        {
            return Task.CompletedTask;
        }

        context.Chat.Send(links.Home is { } home
            ? $"Rangliste, Kämpfe und Trainer: {home}"
            : "Die PokeArena-Website ist noch nicht eingerichtet.");
        return Task.CompletedTask;
    }
}
