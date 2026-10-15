using PokeArena.Core.Data;

namespace PokeArena.Core.Game.Battles;

/// <param name="TypeFactor">The type factor of the hit, for „sehr effektiv“ in the log.</param>
public readonly record struct Hit(int Damage, bool Critical, double TypeFactor);

/// <summary>
/// Damage of one hit (spec §5, ADR-0006):
/// <c>max(1, round(Skalierung × Quote × Typfaktor × Streuung × Krit))</c> with
/// <c>Quote = max((ATK_a + k) / (DEF_d + k), (SPEZ_a + k) / (SPEZ_d + k))</c>.
/// </summary>
public sealed class DamageCalculator(BattleSettings settings, TypeChart types)
{
    public BattleSettings Settings => settings;

    /// <summary>Draws spread, then crit — in that order, so a seed always gives the same hits.</summary>
    public Hit Hit(Fighter attacker, Fighter defender, BattleRandom random)
    {
        var k = settings.Damping;
        var ratio = Math.Max((attacker.Atk + k) / (defender.Def + k), (attacker.Spez + k) / (defender.Spez + k));
        var typeFactor = types.Effectiveness(attacker.Species.Types, defender.Species.Types);
        var spread = random.Between(settings.SpreadMin, settings.SpreadMax);
        var critical = random.Chance(settings.CritChance);
        var raw = settings.Scaling * ratio * typeFactor * spread * (critical ? settings.CritFactor : 1.0);
        var damage = Math.Max(1, (int)Math.Round(raw, MidpointRounding.AwayFromZero));
        return new Hit(damage, critical, typeFactor);
    }
}
