using PokeArena.Core.Game;

namespace PokeArena.Core.Commands;

/// <summary><c>!boss</c>: opens a boss lobby or joins the open one (spec §4). Arguments are ignored.</summary>
public sealed class BossCommand(BossEncounter boss) : IChatCommand
{
    public string Name => "boss";

    public CommandAccess Access => CommandAccess.Registered;

    public Task ExecuteAsync(CommandContext context, CancellationToken ct) => boss.JoinAsync(context.Message, ct);
}
