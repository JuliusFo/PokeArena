using Microsoft.EntityFrameworkCore;
using PokeArena.Core.Chat;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;

namespace PokeArena.Core.Game;

/// <summary>
/// Test viewers for the dry run: their user id is derived from the name typed into the dry-run form, and the
/// streamer can top one up to a full PvP team (WP-04 user decision) or to an NPC's threshold (WP-05) without
/// playing boss fights first. Works on the sandbox only; the work runs on the command queue like every other
/// game change.
/// </summary>
public sealed class DryRunTrainers(
    GameDatabase database,
    SpeciesCatalog species,
    NpcCatalog npcs,
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

    /// <summary>The NPCs a test viewer can be unlocked for, by ascending threshold.</summary>
    public IReadOnlyList<Npc> Npcs => npcs.All;

    /// <summary>
    /// Queues: register <paramref name="displayName"/> with a random starter if needed, then add random specimens
    /// up to <see cref="TeamSize"/>. Does nothing if the dry run is off by the time the work runs.
    /// </summary>
    public void Prepare(string displayName) =>
        inbox.Post(new QueuedWork("Testzuschauer", ct => PrepareAsync(displayName.Trim(), ct)));

    /// <summary>
    /// Queues: like <see cref="Prepare"/>, then add species the viewer lacks until they own as many distinct
    /// species as <paramref name="npc"/> needs, and unlock the achievements reached.
    /// </summary>
    public void Unlock(string displayName, Npc npc) =>
        inbox.Post(new QueuedWork("Testzuschauer", ct => UnlockAsync(displayName.Trim(), npc, ct)));

    private async Task PrepareAsync(string displayName, CancellationToken ct)
    {
        if (!database.IsSandbox)
        {
            return;
        }

        // Test data only, but still no unseeded randomness in Core (ADR-0004).
        var random = new BattleRandom(seeds.NextSeed());
        var userId = await RegisterAsync(displayName, random, ct);

        await using var db = database.Open();
        var owned = await db.Specimens.CountAsync(s => s.TrainerId == userId, ct);
        var now = time.GetUtcNow();
        db.Specimens.AddRange(Enumerable.Range(0, Math.Max(0, TeamSize - owned)).Select(_ => NewSpecimen(userId, random.Pick(species.All), now)));
        await db.SaveChangesAsync(ct);

        trainers.NotifyChanged();
        chatLog.Add(ChatLogKind.Info, null, $"Testzuschauer {displayName} hat jetzt {Math.Max(owned, TeamSize)} Pokémon.", dryRun: true);
    }

    private async Task UnlockAsync(string displayName, Npc npc, CancellationToken ct)
    {
        if (!database.IsSandbox)
        {
            return;
        }

        var random = new BattleRandom(seeds.NextSeed());
        var userId = await RegisterAsync(displayName, random, ct);

        await using var db = database.Open();
        var owned = await db.Specimens.Where(s => s.TrainerId == userId).Select(s => s.SpeciesId).ToListAsync(ct);
        var distinct = owned.ToHashSet();
        var now = time.GetUtcNow();
        var added = random.Shuffled(species.All.Where(s => !distinct.Contains(s.Id)))
            .Take(Math.Max(0, npc.Threshold - distinct.Count))
            .Select(s => NewSpecimen(userId, s, now))
            .ToList();
        added.AddRange(Enumerable.Range(0, Math.Max(0, TeamSize - owned.Count - added.Count)).Select(_ => NewSpecimen(userId, random.Pick(species.All), now)));
        db.Specimens.AddRange(added);
        await db.SaveChangesAsync(ct);
        await Achievements.UnlockNewAsync(db, [userId], npcs, now, ct);
        await db.SaveChangesAsync(ct);

        trainers.NotifyChanged();
        chatLog.Add(
            ChatLogKind.Info,
            null,
            $"Testzuschauer {displayName} hat jetzt {Math.Max(distinct.Count, npc.Threshold)} verschiedene Pokémon und kann {npc.Name} herausfordern.",
            dryRun: true);
    }

    /// <returns>The user id of the (new or existing) test viewer.</returns>
    private async Task<string> RegisterAsync(string displayName, BattleRandom random, CancellationToken ct)
    {
        var userId = UserId(displayName);
        await trainers.RegisterAsync(userId, Login(displayName), displayName, random.Pick(species.Starters), ct);
        return userId;
    }

    private static Specimen NewSpecimen(string userId, Species s, DateTimeOffset now) =>
        new() { TrainerId = userId, SpeciesId = s.Id, CaughtAt = now, Origin = SpecimenOrigin.Boss };
}
