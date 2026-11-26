using Microsoft.EntityFrameworkCore;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;

namespace PokeArena.Core.Game;

/// <summary>
/// Keeps <see cref="SeenSpecies"/> in step with the fights: every species in a fight's log counts as seen for every
/// trainer who took part, sent in or not (spec §9). Called by each kind of fight in the transaction that stores it,
/// after the battle's first save — the rows name the battle by its id.
/// </summary>
public static class Sightings
{
    /// <summary>
    /// Adds the pairs of <paramref name="trainerIds"/> × the species in <paramref name="log"/> not stored yet, and
    /// makes <paramref name="battle"/> the latest fight of every pair (the first one of a new pair).
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="battle"/> is not saved yet.</exception>
    public static async Task RecordAsync(PokeArenaDb db, IEnumerable<string> trainerIds, Battle battle, BattleLog log, CancellationToken ct)
    {
        if (battle.Id == 0)
        {
            throw new InvalidOperationException("Save the battle first: sightings name it by its id.");
        }

        var trainers = trainerIds.Distinct().ToList();
        var species = log.Fighters.Select(f => f.SpeciesId).Distinct().ToList();
        var known = (await db.SeenSpecies
                .Where(s => trainers.Contains(s.TrainerId) && species.Contains(s.SpeciesId))
                .ToListAsync(ct))
            .ToDictionary(s => (s.TrainerId, s.SpeciesId));
        foreach (var trainer in trainers)
        {
            foreach (var id in species)
            {
                if (!known.TryGetValue((trainer, id), out var seen))
                {
                    seen = new SeenSpecies { TrainerId = trainer, SpeciesId = id };
                    db.SeenSpecies.Add(seen);
                }

                if (seen.FirstSeenAt is null)
                {
                    (seen.FirstSeenAt, seen.FirstBattleId) = (battle.FoughtAt, battle.Id);
                }

                (seen.LastSeenAt, seen.LastBattleId) = (battle.FoughtAt, battle.Id);
            }
        }
    }
}
