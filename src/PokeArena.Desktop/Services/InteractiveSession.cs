using System.Runtime.InteropServices;

namespace PokeArena.Desktop.Services;

/// <summary>
/// Whether this process's Windows session has someone connected. The BlazorWebView's
/// <c>WebView2CompositionControl</c> creates a Direct3D 9 device when it loads; in a disconnected remote desktop
/// session that fails with <see cref="GraphicsDeviceUnavailable"/>, leaving an empty window (fix/wpf-software-rendering).
/// </summary>
public static class InteractiveSession
{
    /// <summary><c>D3DERR_NOTAVAILABLE</c>, thrown by the WebView2 control as a <see cref="COMException"/>.</summary>
    public const int GraphicsDeviceUnavailable = unchecked((int)0x8876086A);

    private const int WtsCurrentSession = -1;
    private const int WtsConnectState = 8;
    private const int WtsActive = 0;

    /// <summary>
    /// True when the session is active (someone logged on and connected, locked or not). Unknown counts as active:
    /// the window then opens as it always did.
    /// </summary>
    public static bool IsActive()
    {
        if (!WTSQuerySessionInformationW(IntPtr.Zero, WtsCurrentSession, WtsConnectState, out var buffer, out var bytes))
        {
            return true;
        }

        try
        {
            return bytes < sizeof(int) || Marshal.ReadInt32(buffer) == WtsActive;
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    public static bool IsGraphicsDeviceUnavailable(Exception exception) =>
        exception is COMException { HResult: GraphicsDeviceUnavailable };

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSQuerySessionInformationW(IntPtr server, int sessionId, int infoClass, out IntPtr buffer, out int bytesReturned);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);
}
