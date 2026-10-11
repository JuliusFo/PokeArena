namespace PokeArena.Core.Secrets;

/// <summary>
/// Secrets such as Twitch tokens and the R2 API token. They never go into the database, config files or the
/// repository (ADR-0002); on Windows they live in the Credential Manager.
/// </summary>
public interface ISecretStore
{
    /// <returns>The secret, or null if none is stored under <paramref name="name"/>.</returns>
    string? Get(string name);

    void Set(string name, string value);

    /// <summary>Removes the secret; does nothing if none is stored.</summary>
    void Delete(string name);
}
