using Microsoft.EntityFrameworkCore;
using PokeArena.Core.Chat;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;

namespace PokeArena.Core.Game;

/// <summary>
/// Test viewers for the dry run: their user id is derived from the name typed into the dry-run form, and the
/// streamer can top one up to a full PvP team without playing boss fights first (WP-04 user decision).
/// Works on the sandbox only; the work runs on the command queue like every other game change.
/// </summary>
public sealed class DryRunTrainers(
    GameDatabase database,
    SpeciesCatalog species,
    TrainerService trainers,
    PvpSettings pvpSettings,
    ISeedSource seeds,
    ChatInbox inbox,
    ChatLog chatLog,
    TimeProvider time)
{
    private const string UserIdPrefix = "probelauf:";

    /// <summary>The Twitch login a simulated viewer gets for <paramref name="displayName"/>.</summary>
    public static string Login(string displayName) => displayName.Trim().ToLowerInvariant();

    /// <summary>The user id of a simulated viewer; never collides with a real (numeric) Twitch id.</summary>
    public static string UserId(string displayName) => UserIdPrefix + Login(displayName);

    /// <summary>Specimens a prepared test viewer owns: one full PvP team.</summary>
    public int TeamSize => pvpSettings.TeamSize;

    /// <summary>
    /// Queues: register <paramref name="displayName"/> with a random starter if needed, then add random specimens
    /// up to <see cref="TeamSize"/>. Does nothing if the dry run is off by the time the work runs.
    /// </summary>
    public void Prepare(string displayName) =>
        inbox.Post(new QueuedWork("Testzuschauer", ct => PrepareAsync(displayName.Trim(), ct)));

    private async Task PrepareAsync(string displayName, CancellationToken ct)
    {
        if (!database.IsSandbox)
        {
            return;
        }

        // Test data only, but still no unseeded randomness in Core (ADR-0004).
        var random = new BattleRandom(seeds.NextSeed());
        var userId = UserId(displayName);
        await trainers.RegisterAsync(userId, Login(displayName), displayName, random.Pick(species.Starters), ct);

        await using var db = database.Open();
        var owned = await db.Specimens.CountAsync(s => s.TrainerId == userId, ct);
        var now = time.GetUtcNow();
        db.Specimens.AddRange(Enumerable.Range(0, Math.Max(0, TeamSize - owned)).Select(_ => new Specimen
        {
            TrainerId = userId,
            SpeciesId = random.Pick(species.All).Id,
            CaughtAt = now,
            Origin = SpecimenOrigin.Boss,
        }));
        await db.SaveChangesAsync(ct);

        trainers.NotifyChanged();
        chatLog.Add(ChatLogKind.Info, null, $"Testzuschauer {displayName} hat jetzt {Math.Max(owned, TeamSize)} Pokémon.", dryRun: true);
    }
}
