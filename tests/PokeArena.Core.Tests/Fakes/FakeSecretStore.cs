using System.Collections.Concurrent;
using PokeArena.Core.Secrets;

namespace PokeArena.Core.Tests.Fakes;

public sealed class FakeSecretStore : ISecretStore
{
    public ConcurrentDictionary<string, string> Values { get; } = new();

    public string? Get(string name) => Values.TryGetValue(name, out var value) ? value : null;

    public void Set(string name, string value) => Values[name] = value;

    public void Delete(string name) => Values.TryRemove(name, out _);
}
