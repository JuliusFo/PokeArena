using PokeArena.Core.Game.Battles;

namespace PokeArena.Core.Tests.Fakes;

/// <summary>Always the same seed, so a test can predict a battle.</summary>
public sealed class FixedSeeds(int seed) : ISeedSource
{
    public int NextSeed() => seed;
}
