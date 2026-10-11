using PokeArena.Desktop.Services;

namespace PokeArena.Integration.Tests;

/// <summary>Round trips through the real Windows Credential Manager under a unique, cleaned-up name.</summary>
public class CredentialManagerSecretStoreTests
{
    private readonly CredentialManagerSecretStore _store = new();

    [Fact]
    public void Stored_secret_can_be_read_and_deleted()
    {
        var name = UniqueName();
        try
        {
            _store.Set(name, "s3cret-ä€");

            Assert.Equal("s3cret-ä€", _store.Get(name));

            _store.Delete(name);

            Assert.Null(_store.Get(name));
        }
        finally
        {
            _store.Delete(name);
        }
    }

    [Fact]
    public void Set_overwrites_an_existing_secret()
    {
        var name = UniqueName();
        try
        {
            _store.Set(name, "old");
            _store.Set(name, "new");

            Assert.Equal("new", _store.Get(name));
        }
        finally
        {
            _store.Delete(name);
        }
    }

    [Fact]
    public void Missing_secret_reads_as_null_and_deletes_without_error()
    {
        var name = UniqueName();

        Assert.Null(_store.Get(name));
        _store.Delete(name);
    }

    private static string UniqueName() => $"integration-test:{Guid.NewGuid():N}";
}
