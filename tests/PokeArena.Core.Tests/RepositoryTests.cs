namespace PokeArena.Core.Tests;

/// <summary>Rules about the repository as a whole, checked on the files themselves.</summary>
public class RepositoryTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void No_official_pokemon_assets_anywhere()
    {
        // ADR-0008: no sprites, artwork, sounds or logos — not even fetched at runtime from a known host.
        string[] mediaExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".ico", ".svg", ".mp3", ".wav", ".ogg"];
        string[] assetHosts = ["pokeapi.co", "raw.githubusercontent.com/PokeAPI", "serebii.net", "bulbagarden.net", "pokemondb.net", "pokemon.com", "pokestadium"];
        var files = SourceFiles().ToList();

        Assert.Empty(files.Where(f => mediaExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)).Select(Relative));
        // Only what ships: tools/GeneratePokemonData.cs reads base stats (not assets) from PokeAPI on purpose (ADR-0005).
        var codeFiles = files.Where(f => Relative(f).StartsWith("src/", StringComparison.Ordinal)
            && Path.GetExtension(f) is ".cs" or ".razor" or ".html" or ".css" or ".js" or ".json");
        Assert.Empty(codeFiles
            .Where(f => assetHosts.Any(host => File.ReadAllText(f).Contains(host, StringComparison.OrdinalIgnoreCase)))
            .Select(Relative));
    }

    /// <summary>What ships or builds: src, data and tools, without build output.</summary>
    private static IEnumerable<string> SourceFiles() =>
        new[] { "src", "data", "tools" }
            .SelectMany(dir => Directory.EnumerateFiles(Path.Combine(Root, dir), "*", SearchOption.AllDirectories))
            .Where(f => !Relative(f).Split('/').Any(part => part is "bin" or "obj"));

    private static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PokeArena.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository root (PokeArena.slnx) not found above the test output.");
    }
}
