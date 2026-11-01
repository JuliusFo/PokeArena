using PokeArena.Core.Secrets;

namespace PokeArena.UI.Tests;

public sealed class MemorySecretStore : ISecretStore
{
    public Dictionary<string, string> Values { get; } = [];

    public string? Get(string name) => Values.GetValueOrDefault(name);

    public void Set(string name, string value) => Values[name] = value;

    public void Delete(string name) => Values.Remove(name);
}
