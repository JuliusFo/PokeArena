# PokeArena.Desktop — pitfalls

Thin WPF host around a `BlazorWebView` (ADR-0007). UI belongs in PokeArena.UI, logic in Core.

- TFM must stay `net10.0-windows10.0.19041.0`: with plain `net10.0-windows` `BlazorWebView` crashes at startup.
- The host's content root is `AppContext.BaseDirectory`: started from a shortcut, the working directory is
  arbitrary and `appsettings.json` would silently not load.
- `--PokeArena:DataDirectory=<path>` redirects logs and data — use it for manual tests and screenshots so the
  real `%LOCALAPPDATA%\PokeArena` stays untouched.
- `WindowTheme.PrepareWebViewBackground()` must run before the WebView2 environment exists (white flash
  otherwise); its colors must match `--bg` in `PokeArena.UI/wwwroot/css/pokearena.css`.
- Single-instance mutex: two running bots would answer every chat command twice — don't remove it.
- Exceptions before the host is built are only visible via `Log.*` (static Serilog) and the start-failure
  message box; keep both. All message box texts are German (the streamer reads them).
- Credentials are stored as `PokeArena:<name>`; integration tests use `integration-test:<guid>` names and
  delete them again.
- Twitch settings live in `appsettings.json` under `PokeArena:Twitch` (client ID is public, ADR-0002). The
  channel for development goes into the git-ignored `appsettings.local.json` next to the csproj; it is copied
  to the output only if it exists. The command line still overrides both.
- The Twitch login is stored in the Credential Manager as `PokeArena:twitch-token` — independent of
  `--PokeArena:DataDirectory`, so a test profile shares the real login.
- `data/*.json` is linked into the output (`data\`); the app refuses to start if a file is missing or
  invalid. Startup order matters: `AddPokeArenaGame` (base data check, migration) before `AddPokeArenaChat`.
- Smoke tests without touching the real chat: `--PokeArena:DataDirectory=<scratch> --PokeArena:Twitch:Channel=`
  (empty channel = not configured, the bot stays offline).
- The OBS overlay (`OverlayServer`) is a second, slim Kestrel host on the loopback interface (`PokeArena:Overlay:Port`,
  default 5299; 0 = off). A busy port only disables the overlay. `Microsoft.AspNetCore` logs at Warning, else the
  2-second polling floods the log file.
- Website settings live under `PokeArena:Website` (site URL, R2 endpoint, bucket — not secret); the R2 key is in the
  Credential Manager as `PokeArena:r2-access-key-id` / `PokeArena:r2-secret-access-key` (docs/website-setup.md).
- Streamer settings (WP-08): `settings.json` in the data directory (written by the „Einstellungen“ card via
  `SettingsFile`) is inserted as a configuration source between `appsettings.json` and `appsettings.local.json`
  (`App.AddStreamerSettings`); it is read once at start, so changes apply after a restart.
- Shutdown: the main window's `Closing` cancels once, awaits `IHost.StopAsync` and then calls `Shutdown()`.
  Never await in `OnExit` — the dispatcher is already shutting down and the continuation never runs (review 1.0.3).
