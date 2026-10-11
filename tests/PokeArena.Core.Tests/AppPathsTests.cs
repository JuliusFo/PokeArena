using PokeArena.Core;

namespace PokeArena.Core.Tests;

public class AppPathsTests
{
    [Fact]
    public void Default_lives_in_local_app_data()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.Equal(Path.Combine(localAppData, "PokeArena"), AppPaths.Default.Root);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_without_data_directory_uses_default(string? dataDirectory)
    {
        var paths = AppPaths.Resolve(new PokeArenaOptions { DataDirectory = dataDirectory });

        Assert.Same(AppPaths.Default, paths);
    }

    [Fact]
    public void Resolve_with_relative_data_directory_makes_it_absolute()
    {
        var paths = AppPaths.Resolve(new PokeArenaOptions { DataDirectory = "profile" });

        Assert.Equal(Path.Combine(Environment.CurrentDirectory, "profile"), paths.Root);
    }

    [Fact]
    public void Logs_directory_is_below_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "pokearena-test");

        Assert.Equal(Path.Combine(root, "logs"), new AppPaths(root).LogsDirectory);
    }
}
