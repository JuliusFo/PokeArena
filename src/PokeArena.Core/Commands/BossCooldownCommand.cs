using System.Globalization;
using PokeArena.Core.Chat;
using PokeArena.Core.Game;
using PokeArena.Core.Game.Battles;

namespace PokeArena.Core.Commands;

/// <summary>
/// <c>!bosscooldown &lt;minutes&gt; [cooldown]</c> (mods): for that many minutes the boss cooldown is the given number of
/// minutes, half of BossCooldownMinutes by default; <c>!bosscooldown aus</c> ends it early (spec §8).
/// </summary>
public sealed class BossCooldownCommand(BossEncounter boss, BossSettings settings) : IChatCommand
{
    public string Name => "bosscooldown";

    public CommandAccess Access => CommandAccess.Moderators;

    public async Task ExecuteAsync(CommandContext context, CancellationToken ct)
    {
        var words = ChatText.Words(context.Arguments);
        var message = context.Message;
        if (words is [var off] && (off.Equals("aus", StringComparison.OrdinalIgnoreCase) || off == "0"))
        {
            if (!await boss.EndReductionAsync(ct))
            {
                context.Chat.Send($"@{message.DisplayName}, der Boss-Cooldown ist gerade nicht verkürzt.");
            }

            return;
        }

        var maxDuration = (int)BossEncounter.MaxReductionDuration.TotalMinutes;
        var maxCooldown = (int)Math.Ceiling(settings.Cooldown.TotalMinutes) - 1;
        if (words.Length is 1 or 2
            && Minutes(words[0], maxDuration) is { } duration
            && (words.Length == 1 ? settings.Cooldown / 2 : Minutes(words[1], maxCooldown)) is { } cooldown)
        {
            await boss.ReduceCooldownAsync(duration, cooldown, ct);
            return;
        }

        // Moderator replies are never throttled (docs/chat-texts.md), like those of !entfernen.
        context.Chat.Send(
            $"@{message.DisplayName}, so geht's: !bosscooldown 60 halbiert den Boss-Cooldown für 60 Minuten, "
            + "!bosscooldown 60 10 setzt ihn so lange auf 10 Minuten, !bosscooldown aus beendet das. "
            + $"Dauer 1 bis {maxDuration} Minuten, Cooldown 1 bis {maxCooldown} Minuten.");
    }

    /// <summary>Whole minutes from 1 to <paramref name="max"/>, or null.</summary>
    private static TimeSpan? Minutes(string word, int max) =>
        int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes >= 1 && minutes <= max
            ? TimeSpan.FromMinutes(minutes)
            : null;
}
