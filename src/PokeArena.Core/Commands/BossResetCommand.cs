using PokeArena.Core.Game;

namespace PokeArena.Core.Commands;

/// <summary><c>!bossreset</c> (mods): clears the global boss cooldown (spec §8). Arguments are ignored.</summary>
public sealed class BossResetCommand(BossEncounter boss) : IChatCommand
{
    public string Name => "bossreset";

    public CommandAccess Access => CommandAccess.Moderators;

    public Task ExecuteAsync(CommandContext context, CancellationToken ct) => boss.ResetCooldownAsync(ct);
}
