using Microsoft.EntityFrameworkCore;
using PokeArena.Core.Data;
using PokeArena.Core.Game.Battles;

namespace PokeArena.Core.Game;

/// <summary>
/// Keeps <see cref="SeenSpecies"/> in step with the fights: every species in a fight's log counts as seen for every
/// trainer who took part, sent in or not (spec §9). Called by each kind of fight in the transaction that stores it.
/// </summary>
public static class Sightings
{
    /// <summary>Adds the pairs of <paramref name="trainerIds"/> × the species in <paramref name="log"/> not stored yet.</summary>
    public static async Task RecordAsync(PokeArenaDb db, IEnumerable<string> trainerIds, BattleLog log, CancellationToken ct)
    {
        var trainers = trainerIds.Distinct().ToList();
        var species = log.Fighters.Select(f => f.SpeciesId).Distinct().ToList();
        var known = await db.SeenSpecies
            .Where(s => trainers.Contains(s.TrainerId) && species.Contains(s.SpeciesId))
            .Select(s => new { s.TrainerId, s.SpeciesId })
            .ToListAsync(ct);
        var stored = known.Select(k => (k.TrainerId, k.SpeciesId)).ToHashSet();
        db.SeenSpecies.AddRange(
            from trainer in trainers
            from id in species
            where !stored.Contains((trainer, id))
            select new SeenSpecies { TrainerId = trainer, SpeciesId = id });
    }
}
