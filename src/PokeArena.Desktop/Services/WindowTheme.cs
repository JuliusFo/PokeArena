using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace PokeArena.Desktop.Services;

/// <summary>
/// Light/dark for the native parts of the window (title bar, background behind the WebView), following Windows.
/// The WebView itself follows Windows through <c>prefers-color-scheme</c>. Colors match <c>--bg</c> in pokearena.css.
/// </summary>
public static class WindowTheme
{
    private const int DwmUseImmersiveDarkMode = 20;
    private static readonly Color DarkBackground = Color.FromRgb(0x14, 0x15, 0x18);
    private static readonly Color LightBackground = Color.FromRgb(0xf4, 0xf4, 0xf6);

    /// <summary>Must run before the WebView2 environment is created, otherwise it starts with a white flash.</summary>
    public static void PrepareWebViewBackground()
    {
        var c = IsSystemDark() ? DarkBackground : LightBackground;
        Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", $"FF{c.R:X2}{c.G:X2}{c.B:X2}");
    }

    public static void Attach(Window window)
    {
        Apply(window);
        window.SourceInitialized += (_, _) => Apply(window);

        UserPreferenceChangedEventHandler handler = (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                window.Dispatcher.Invoke(() => Apply(window));
            }
        };
        SystemEvents.UserPreferenceChanged += handler;
        window.Closed += (_, _) => SystemEvents.UserPreferenceChanged -= handler;
    }

    private static void Apply(Window window)
    {
        var dark = IsSystemDark();
        window.Background = new SolidColorBrush(dark ? DarkBackground : LightBackground);

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            var value = dark ? 1 : 0;
            _ = DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref value, sizeof(int));
        }
    }

    private static bool IsSystemDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is 0;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
