namespace PokeArena.Core.Game.Battles;

/// <summary>
/// Balancing values of the battle engine with the defaults of spec §13 (German key names in the comments). Fixed
/// in code for now; editing them at runtime in the UI is a later package.
/// </summary>
public sealed record BattleSettings
{
    public static BattleSettings Default { get; } = new();

    /// <summary>Skalierung: base damage of a hit with ratio 1 and type factor 1.</summary>
    public double Scaling { get; init; } = 25;

    /// <summary>Daempfung (k): added to both sides of the attack/defence ratio (ADR-0006).</summary>
    public double Damping { get; init; } = 50;

    /// <summary>Streuung: each hit is multiplied by a uniform factor in [min, max).</summary>
    public double SpreadMin { get; init; } = 0.85;

    public double SpreadMax { get; init; } = 1.00;

    /// <summary>KritChance.</summary>
    public double CritChance { get; init; } = 0.0625;

    /// <summary>KritFaktor.</summary>
    public double CritFactor { get; init; } = 2.0;

    /// <summary>HPFaktor: effective HP = base HP × factor, in every fight.</summary>
    public double HpFactor { get; init; } = 1.0;

    /// <summary>MaxRunden: pure safety net — reaching it is a bug (ADR-0004).</summary>
    public int MaxRounds { get; init; } = 200;
}

/// <summary>Boss values of spec §4 and §13.</summary>
public sealed record BossSettings
{
    public static BossSettings Default { get; } = new();

    /// <summary>BossCountdownSeconds: how long the lobby stays open.</summary>
    public TimeSpan Countdown { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>BossMinTeilnehmer.</summary>
    public int MinParticipants { get; init; } = 2;

    /// <summary>BossCooldownMinutes: starts when an encounter ends.</summary>
    public TimeSpan Cooldown { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>BossFehlschlagCooldownMinutes: after a lobby with too few participants.</summary>
    public TimeSpan FailedLobbyCooldown { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>BossHPProSpieler: boss HP = base HP × HPFaktor × this × participants.</summary>
    public double HpPerParticipant { get; init; } = 1.8;

    /// <summary>BossZieleTeiler: the boss attacks ⌈N / this⌉ participants per round.</summary>
    public int TargetDivisor { get; init; } = 3;

    /// <summary>Legendary tier: min(cap, base + per participant × N).</summary>
    public TierOdds Legendary { get; init; } = new(0.01, 0.01, 0.20);

    /// <summary>Rare tier: min(cap, base + per participant × N); the rest is common.</summary>
    public TierOdds Rare { get; init; } = new(0.10, 0.02, 0.40);
}

/// <summary>Probability of a boss tier, growing with the participant count up to a cap.</summary>
public sealed record TierOdds(double Base, double PerParticipant, double Cap)
{
    public double For(int participants) => Math.Min(Cap, Base + PerParticipant * participants);
}
