# Empty window after a restart without a session — user report (2026-11-23, branch fix/wpf-software-rendering)

- User report: screenshot of the desktop app on the server with an empty window and two boxes „Ein unerwarteter
  Fehler ist aufgetreten: 0x8876086A“ (`OnDispatcherUnhandledException`), on the day 1.10.0 installed itself.
- Cause (from the code, the server log's stack trace was not available): BlazorWebView 10 is a
  `WebView2CompositionControl`; `TryInitializeD3DImage` (from `OnApplyTemplate` and `Loaded`) creates a Direct3D 9 HAL
  device through `Marshal.ThrowExceptionForHR`. In a disconnected RDP session — the restart after an unattended
  self-update — that fails with `D3DERR_NOTAVAILABLE`, whose .NET message is exactly „0x8876086A“; two call sites, two
  boxes, and the control never retries, so the window stays empty. The bot itself kept running (host starts first).
- Built: `InteractiveSession` (wtsapi32 `WTSConnectState`; unknown counts as active) and the HRESULT check.
  `App.StartAsync` shows the window only in an active session, else logs and waits for `SystemEvents.SessionSwitch`.
  `OnDispatcherUnhandledException` handles 0x8876086A without a box (logs once) and on the next connection calls
  `MainWindow.RebuildWebViewAsync`; the BlazorWebView is therefore built in code instead of XAML. Integration tests:
  the HRESULT check against `Marshal.GetExceptionForHR` (message „0x8876086A“) and the real session query.
- User decisions: first „Ja, umstellen“ to WPF software rendering (`RenderMode.SoftwareOnly`) on my diagnosis of a WPF
  render-thread failure. The code review showed that diagnosis wrong; then „Alle, Variante A“: no software rendering,
  window only in an active session, no box for this error, rebuild on the next connection.
- Code review (2026-11-23): „Alle, Variante A“ (no package, so also in the PR description).
  - 1 M, fix did not reach the failing code: fixed as above (`SoftwareOnly` removed).
  - 2 M, docs claimed WebView2 renders unaffected by WPF: fixed, Desktop pitfall rewritten.
  - 3 L, symptom described as „one box per frame“: fixed (two boxes) in CHANGELOG and this log.
  - 4 L, version column „next“: fixed, `1.10.1 (proposal)`.
  - 5 L, no recorded check: fixed, see „Tested“.
  - Pre-existing: other UI-thread errors still open a modal box each → docs/backlog.md (Operations).
  - Second pass, „A, B, C beheben“:
    - A M, a failed load in an active session ended silently (no further `SessionSwitch`): fixed, `ScheduleReload`
      tries again after 2, 5 and 15 s (the device may come a moment after the connection), then logs an error, shows
      a German hint in the window (`MainWindow.ShowLoadFailedHint`) and waits for the next connection.
    - B L, a loaded WebView would be dropped undisposed: fixed, `BlazorWebViewInitialized` marks the current one as
      loaded; `RebuildWebViewAsync` disposes such a one first (at most 5 s), a never-loaded one is still dropped.
    - C L, exceptions in the `InvokeAsync` session handler would vanish: fixed, `BeginInvoke`.
    - D L, decision logic untested: declined (user, „A, B, C“); covered by the forced-error runs below instead.
- Tested: build with `-warnaserror`, all tests green; a WPF window never shown still raises `Closing` on `Close()`
  (checked in a scratch program), so the self-update can end the app while the window waits; local smoke start in an
  active session: window and UI appear as before. Two local runs with a temporary, uncommitted throw of the real
  `COMException` 0x8876086A: on every load → retries at 2, 5, 15 s, then the error line and the hint in the window,
  no box; once, 3 s after a successful load → the loaded WebView disposed, rebuilt after 2 s, UI back, one WebView2
  browser process.
- Not tested: a real restart in a disconnected RDP session on the server, and the `SessionSwitch` path (no way to
  disconnect the session locally). To watch after the update: the log line „No one is connected to this Windows
  session“ after a self-update, and the window appearing on the next RDP connection.
- Pitfalls: BlazorWebView's WebView2 is composited by WPF (`D3DImage`), not a child window — WPF's render mode and
  Direct3D state concern the whole UI.
