# WPF software rendering — user report (2026-11-23, branch fix/wpf-software-rendering)

- Built: `Program.Main` sets `RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly` before the `App` exists.
  CHANGELOG „Fixed“, Desktop pitfall line. No „Neuigkeiten“ line: players do not notice it.
- User report: screenshot of the desktop app with an empty window and two boxes „Ein unerwarteter Fehler ist
  aufgetreten: 0x8876086A“ (`OnDispatcherUnhandledException`, one per failed frame). 0x8876086A is
  `D3DERR_NOTAVAILABLE`: WPF's render thread got no Direct3D device — the app runs on the user's server (no real GPU,
  usually reached over RDP; a Velopack restart may happen with the session disconnected). Microsoft's first step for
  WPF render thread failures is turning hardware acceleration off.
- User decisions: „Ja, umstellen“ (software rendering in code instead of only the per-user registry value
  `HKCU\Software\Microsoft\Avalon.Graphics\DisableHWAcceleration`).
- Pitfalls: the setting covers only WPF's own drawing (window frame, message boxes); the `BlazorWebView` is a
  WebView2 child window that renders itself, so the UI keeps its GPU path where one exists.
- Not verified: the server log's stack trace was not available — the diagnosis rests on the HRESULT. If the boxes come
  back, read „Unhandled exception on UI thread“ in `%LOCALAPPDATA%\PokeArena\logs` on the server.
