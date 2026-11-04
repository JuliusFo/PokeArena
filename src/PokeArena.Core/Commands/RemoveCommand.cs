using PokeArena.Core.Game;

namespace PokeArena.Core.Commands;

/// <summary><c>!entfernen @name</c> (mods): deletes a trainer after a confirmation (spec §8, §11).</summary>
public sealed class RemoveCommand(TrainerRemoval removal) : IChatCommand
{
    public string Name => "entfernen";

    public CommandAccess Access => CommandAccess.Moderators;

    public Task ExecuteAsync(CommandContext context, CancellationToken ct) =>
        removal.RequestAsync(context.Message, context.Arguments, ct);
}
