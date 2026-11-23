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
        // Off: by default Velopack installs a downloaded update on the next start — after a crash or a restart for new
        // settings that would be mid-stream. Only UpdateCheck installs, when UpdateGate allows (review WP-11).
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
