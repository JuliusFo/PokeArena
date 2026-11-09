using PokeArena.Core.Data;

namespace PokeArena.Core.Game.Battles;

/// <summary>The boss of an encounter (spec §4), drawn once the lobby closes and the participant count is known.</summary>
public static class BossDraw
{
    /// <summary>
    /// Tier first — legendary and rare grow with <paramref name="participants"/> up to their caps, the rest is
    /// common — then a uniform species within the tier.
    /// </summary>
    public static Species Species(SpeciesCatalog species, BossSettings settings, int participants, BattleRandom random)
    {
        var roll = random.NextDouble();
        var legendary = settings.Legendary.For(participants);
        var rare = settings.Rare.For(participants);
        var tier = roll < legendary ? 3 : roll < legendary + rare ? 2 : 1;
        return random.Pick(species.All.Where(s => s.Rarity == tier).ToList());
    }

    /// <summary>
    /// The species a participant fields: uniform over the distinct species they own, so duplicates (early, mostly
    /// common catches) do not crowd out the few rare ones. Specimens of one species fight identically, so the
    /// species is all the fight needs. Sorted by id, so a seed always means the same pick.
    /// </summary>
    public static int FieldedSpecies(IEnumerable<int> ownedSpeciesIds, BattleRandom random) =>
        random.Pick(ownedSpeciesIds.Distinct().Order().ToList());

    /// <summary>The boss as a fighter: HP = base HP × HpFactor × BossHpPerParticipant × N, other stats are base values.</summary>
    public static Fighter Fighter(Species boss, BattleSettings battle, BossSettings settings, int participants) =>
        Battles.Fighter.Create(boss, battle, trainerId: null, owner: "Boss", hpMultiplier: settings.HpPerParticipant * participants);
}
