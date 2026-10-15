using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace PokeArena.Core.Game.Battles;

/// <summary>Hands out the seed of each battle (ADR-0004). Tests use fixed seeds.</summary>
public interface ISeedSource
{
    int NextSeed();
}

/// <summary>Seeds from the operating system's random generator.</summary>
public sealed class SystemSeedSource : ISeedSource
{
    public int NextSeed() => RandomNumberGenerator.GetInt32(int.MaxValue);
}

/// <summary>
/// The only randomness of a battle: everything drawn for one encounter (boss, specimens, hits) comes from this
/// seeded generator, so the stored seed explains the outcome under the settings of that time.
/// </summary>
public sealed class BattleRandom(int seed)
{
    private readonly Random _random = new(seed);

    public int Seed => seed;

    /// <summary>Uniform in [0, 1).</summary>
    public double NextDouble() => _random.NextDouble();

    /// <summary>Uniform in [<paramref name="min"/>, <paramref name="max"/>).</summary>
    public double Between(double min, double max) => min + (max - min) * _random.NextDouble();

    public bool Chance(double probability) => _random.NextDouble() < probability;

    public T Pick<T>(IReadOnlyList<T> items) => items[_random.Next(items.Count)];

    public List<T> Shuffled<T>(IEnumerable<T> items)
    {
        var list = items.ToList();
        _random.Shuffle(CollectionsMarshal.AsSpan(list));
        return list;
    }
}
