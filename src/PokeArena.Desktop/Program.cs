using Velopack;

namespace PokeArena.Desktop;

/// <summary>
/// Own entry point instead of the one WPF generates from App.xaml (WP-11): Velopack starts the program with special
/// arguments during install, update and uninstall and must handle them before anything else runs — before the
/// single-instance mutex, the log and the host (ADR-0010).
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main()
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
