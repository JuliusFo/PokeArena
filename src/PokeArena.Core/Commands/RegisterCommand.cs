using PokeArena.Core.Data;
using PokeArena.Core.Game;

namespace PokeArena.Core.Commands;

/// <summary><c>!registrieren &lt;Starter&gt;</c>: one-time, irrevocable registration with one of five starters (spec §3).</summary>
public sealed class RegisterCommand(TrainerService trainers, SpeciesCatalog species) : IChatCommand
{
    public string Name => "registrieren";

    public CommandAccess Access => CommandAccess.Everyone;

    public async Task ExecuteAsync(CommandContext context, CancellationToken ct)
    {
        var mention = "@" + context.Message.DisplayName;

        // Only the first word counts: "!registrieren Pikachu bitte" still works.
        var choice = context.Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        if (species.FindStarter(choice) is not { } starter)
        {
            var names = species.Starters.Select(s => s.Name).ToList();
            context.Chat.Send(
                $"{mention}, wähle deinen Starter: {string.Join(", ", names[..^1])} oder {names[^1]} – " +
                "zum Beispiel: !registrieren Pikachu");
            return;
        }

        var result = await trainers.RegisterAsync(context.Message.UserId, context.Message.DisplayName, starter, ct);
        context.Chat.Send(result.Status == RegistrationStatus.Registered
            ? $"{mention}, willkommen bei PokeArena! Dein Starter ist {result.Starter.Name}."
            : $"{mention}, du bist schon dabei – dein Starter ist {result.Starter.Name}.");
    }
}
