using PokeArena.Core.Game;

namespace PokeArena.Core.Commands;

/// <summary><c>!challenge &lt;NPC&gt;</c>: fights an NPC the trainer has unlocked (spec §7).</summary>
public sealed class ChallengeCommand(NpcService npcs) : IChatCommand
{
    public string Name => "challenge";

    public CommandAccess Access => CommandAccess.Registered;

    public Task ExecuteAsync(CommandContext context, CancellationToken ct) => npcs.ChallengeAsync(context.Message, context.Arguments, ct);
}
