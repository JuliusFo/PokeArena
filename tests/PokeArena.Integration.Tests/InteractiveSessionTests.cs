using System.Runtime.InteropServices;
using PokeArena.Desktop.Services;

namespace PokeArena.Integration.Tests;

/// <summary>The session query against the real Remote Desktop API and the WebView's graphics-device failure.</summary>
public sealed class InteractiveSessionTests
{
    [Fact]
    public void Recognises_the_exception_the_webview_throws_without_a_graphics_device()
    {
        // The WebView2 control turns D3DERR_NOTAVAILABLE into an exception via Marshal.ThrowExceptionForHR; its
        // message is just "0x8876086A" — the text of the boxes in the user's report.
        var exception = Marshal.GetExceptionForHR(InteractiveSession.GraphicsDeviceUnavailable)!;

        Assert.True(InteractiveSession.IsGraphicsDeviceUnavailable(exception));
        Assert.Equal("0x8876086A", exception.Message);
    }

    [Fact]
    public void Does_not_swallow_other_errors()
    {
        Assert.False(InteractiveSession.IsGraphicsDeviceUnavailable(Marshal.GetExceptionForHR(unchecked((int)0x88980406))!));
        Assert.False(InteractiveSession.IsGraphicsDeviceUnavailable(new InvalidOperationException("0x8876086A")));
    }

    [Fact]
    public void Queries_the_session_state_without_failing()
    {
        // Active or not depends on how the tests run (desktop, CI service); the call itself must work.
        _ = InteractiveSession.IsActive();
    }
}
