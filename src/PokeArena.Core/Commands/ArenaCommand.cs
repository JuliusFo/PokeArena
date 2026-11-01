using PokeArena.Core.Publishing;

namespace PokeArena.Core.Commands;

/// <summary><c>!arena</c>: posts the website link (spec §8). Same short global cooldown as <c>!pokearena</c>.</summary>
public sealed class ArenaCommand(SiteLinks links, TimeProvider time) : IChatCommand
{
    private DateTimeOffset? _lastReply;

    public string Name => "arena";

    public CommandAccess Access => CommandAccess.Everyone;

    public Task ExecuteAsync(CommandContext context, CancellationToken ct)
    {
        if (!context.Message.IsSimulated)
        {
            var now = time.GetUtcNow();
            if (_lastReply is { } last && now - last < StatusCommand.Cooldown)
            {
                return Task.CompletedTask;
            }

            _lastReply = now;
        }

        context.Chat.Send(links.Home is { } home
            ? $"Rangliste, Kämpfe und Trainer: {home}"
            : "Die PokeArena-Website ist noch nicht eingerichtet.");
        return Task.CompletedTask;
    }
}
