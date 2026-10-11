using Meziantou.Framework.Win32;
using PokeArena.Core.Secrets;

namespace PokeArena.Desktop.Services;

/// <summary>Stores secrets as generic credentials in the Windows Credential Manager ("PokeArena:&lt;name&gt;").</summary>
public sealed class CredentialManagerSecretStore : ISecretStore
{
    private const string TargetPrefix = "PokeArena:";

    public string? Get(string name) => CredentialManager.ReadCredential(Target(name))?.Password;

    public void Set(string name, string value) =>
        CredentialManager.WriteCredential(
            Target(name),
            "PokeArena",
            value,
            "PokeArena bot secret",
            CredentialPersistence.LocalMachine);

    public void Delete(string name) => CredentialManager.TryDeleteCredential(Target(name));

    private static string Target(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return TargetPrefix + name;
    }
}
