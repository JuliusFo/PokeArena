using System.Windows.Interop;
using System.Windows.Media;
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

        // WPF draws only the window frame here (the UI is WebView2, which renders on its own). On the server without
        // a real GPU, or in a disconnected RDP session, WPF's Direct3D render thread failed with 0x8876086A
        // (D3DERR_NOTAVAILABLE): an empty window and one error box per frame. Software rendering avoids Direct3D.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
