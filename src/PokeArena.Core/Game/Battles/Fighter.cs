using PokeArena.Core.Data;

namespace PokeArena.Core.Game.Battles;

/// <summary>A Pokemon as it enters one fight: effective stats, fixed for the fight. HP is tracked by the engine.</summary>
/// <param name="TrainerId">Owner's Twitch user id; null for a boss or an NPC.</param>
/// <param name="Owner">Shown in the log: a trainer's display name, „Boss“ or an NPC name.</param>
public sealed record Fighter(Species Species, int MaxHp, double Atk, double Def, double Spez, string? TrainerId, string Owner)
{
    /// <summary>
    /// Effective values: HP = base HP × HPFaktor × <paramref name="hpMultiplier"/> (boss scaling);
    /// <paramref name="strength"/> scales every stat (NPC strength, spec §7).
    /// </summary>
    public static Fighter Create(
        Species species,
        BattleSettings settings,
        string? trainerId,
        string owner,
        double hpMultiplier = 1.0,
        double strength = 1.0)
    {
        var hp = (int)Math.Round(species.Hp * settings.HpFactor * hpMultiplier * strength, MidpointRounding.AwayFromZero);
        return new Fighter(
            species,
            Math.Max(1, hp),
            species.Atk * strength,
            species.Def * strength,
            species.Spez * strength,
            trainerId,
            owner);
    }
}
