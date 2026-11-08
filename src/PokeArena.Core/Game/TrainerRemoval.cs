using Microsoft.Extensions.Logging;
using PokeArena.Core.Chat;

namespace PokeArena.Core.Game;

/// <summary>
/// Deleting trainers (spec §11): <c>!entfernen @name</c> from a moderator, confirmed by the same moderator with
/// <c>!entfernen @name ja</c> (WP-08 decision — a typo must not delete the wrong viewer), and the confirmed button in
/// the desktop app. Runs on the command queue, so the pending confirmations need no lock.
/// </summary>
public sealed class TrainerRemoval(
    TrainerService trainers,
    BattleService battles,
    IChatSender chat,
    TimeProvider time,
    ILogger<TrainerRemoval> log)
{
    public static readonly TimeSpan ConfirmationTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Moderator user id → the deletion they asked for.</summary>
    private readonly Dictionary<string, Pending> _pending = [];

    /// <summary><c>!entfernen</c> from a moderator: asks for confirmation, or deletes if this confirms a request.</summary>
    public async Task RequestAsync(ChatMessage message, string arguments, CancellationToken ct)
    {
        var mention = "@" + message.DisplayName;
        var words = ChatText.Words(arguments);
        if (words.Length == 0)
        {
            chat.Send($"{mention}, wen soll ich löschen? Zum Beispiel: !entfernen @name");
            return;
        }

        var match = await trainers.ResolveNameAsync(words[0], ct);
        if (match.Trainer is not { } target)
        {
            var typed = ChatText.Echo(words[0]);
            chat.Send(match.Ambiguous
                ? $"{mention}, mehrere Trainer heißen {typed ?? "so"} – bitte in der PokeArena-App löschen."
                : typed is null ? $"{mention}, diesen Trainer finde ich nicht." : $"{mention}, ich finde keinen Trainer namens {typed}.");
            return;
        }

        var now = time.GetUtcNow();
        var confirms = words.Length > 1 && words[1].Equals("ja", StringComparison.OrdinalIgnoreCase);
        if (confirms
            && _pending.Remove(message.UserId, out var pending)
            && pending.TrainerId == target.TwitchUserId
            && pending.IsSimulated == message.IsSimulated
            && now < pending.ExpiresAt)
        {
            log.LogInformation("Moderator {ModeratorId} deletes trainer {UserId}", message.UserId, target.TwitchUserId);
            await DeleteAsync(target.TwitchUserId, ct);
            chat.Send($"{target.DisplayName} wurde mit allen Pokémon gelöscht.");
            return;
        }

        _pending[message.UserId] = new Pending(target.TwitchUserId, message.IsSimulated, now + ConfirmationTimeout);
        chat.Send(
            $"{mention}, wirklich {target.DisplayName} mit allen Pokémon löschen? Das lässt sich nicht rückgängig machen. " +
            $"Bestätige innerhalb von {ConfirmationTimeout.TotalSeconds:0} Sekunden mit !entfernen @{target.Handle} ja");
    }

    /// <summary>Deletes right away — the desktop app asks for confirmation itself.</summary>
    /// <returns>False if the trainer was already gone.</returns>
    public async Task<bool> DeleteAsync(string twitchUserId, CancellationToken ct)
    {
        var deleted = await trainers.DeleteAsync(twitchUserId, ct);
        if (deleted)
        {
            battles.NotifyChanged();
        }

        return deleted;
    }

    /// <param name="IsSimulated">Asked in the dry run; only a dry-run message confirms it.</param>
    private sealed record Pending(string TrainerId, bool IsSimulated, DateTimeOffset ExpiresAt);
}
