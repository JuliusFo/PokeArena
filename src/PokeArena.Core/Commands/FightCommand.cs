using PokeArena.Core.Game;

namespace PokeArena.Core.Commands;

/// <summary><c>!fight @name</c>: challenges a trainer, or accepts that trainer's open challenge (spec §6).</summary>
public sealed class FightCommand(PvpService pvp) : IChatCommand
{
    public string Name => "fight";

    public CommandAccess Access => CommandAccess.Registered;

    public Task ExecuteAsync(CommandContext context, CancellationToken ct) => pvp.FightAsync(context.Message, context.Arguments, ct);
}
