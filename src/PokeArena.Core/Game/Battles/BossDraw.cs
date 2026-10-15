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

    /// <summary>The boss as a fighter: HP = base HP × HPFaktor × BossHPProSpieler × N, other stats are base values.</summary>
    public static Fighter Fighter(Species boss, BattleSettings battle, BossSettings settings, int participants) =>
        Battles.Fighter.Create(boss, battle, trainerId: null, owner: "Boss", hpMultiplier: settings.HpPerParticipant * participants);
}
