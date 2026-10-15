using Microsoft.EntityFrameworkCore;
using PokeArena.Core.Data;

namespace PokeArena.Core.Game;

/// <summary>An achievement reached just now; announced in chat with the NPC it unlocks.</summary>
public sealed record NewAchievement(string TrainerId, string DisplayName, Npc Npc);

/// <summary>
/// Achievements (spec §1): thresholds of distinct species owned. Checked after every gain of specimens; once
/// unlocked they stay, even if the count drops later.
/// </summary>
public static class Achievements
{
    /// <summary>
    /// Adds unlocks for every threshold the given trainers have reached but not yet unlocked. Works on the caller's
    /// context and transaction; the specimens gained must already be saved.
    /// </summary>
    public static async Task<IReadOnlyList<NewAchievement>> UnlockNewAsync(
        PokeArenaDb db,
        IReadOnlyCollection<string> trainerIds,
        NpcCatalog npcs,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (trainerIds.Count == 0)
        {
            return [];
        }

        var counts = await db.Trainers
            .Where(t => trainerIds.Contains(t.TwitchUserId))
            .Select(t => new
            {
                t.TwitchUserId,
                t.DisplayName,
                Distinct = t.Specimens.Select(s => s.SpeciesId).Distinct().Count(),
            })
            .ToListAsync(ct);
        var unlocked = (await db.AchievementUnlocks
                .Where(u => trainerIds.Contains(u.TrainerId))
                .Select(u => new { u.TrainerId, u.Threshold })
                .ToListAsync(ct))
            .Select(u => (u.TrainerId, u.Threshold))
            .ToHashSet();

        var reached = new List<NewAchievement>();
        foreach (var trainer in counts)
        {
            foreach (var threshold in npcs.ThresholdsReached(trainer.Distinct))
            {
                if (unlocked.Contains((trainer.TwitchUserId, threshold)))
                {
                    continue;
                }

                db.AchievementUnlocks.Add(new AchievementUnlock { TrainerId = trainer.TwitchUserId, Threshold = threshold, UnlockedAt = now });
                reached.Add(new NewAchievement(trainer.TwitchUserId, trainer.DisplayName, npcs.ForThreshold(threshold)));
            }
        }

        return reached;
    }
}
