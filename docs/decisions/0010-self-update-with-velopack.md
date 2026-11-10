# ADR-0010: The desktop app updates itself with Velopack

Status: accepted · Date: 2026-11-10

## Context

Since WP-08 every release was a self-contained ZIP: close the app, download,
replace the program folder. The bot now runs on the user's Windows server
(2026-11-10, apparently for good), so every release meant signing in to the
server and copying the ZIP by hand. The app already kept everything that must
survive an update outside the program folder: database, backups, logs and
`settings.json` in `%LOCALAPPDATA%\PokeArena`, secrets in the Credential
Manager.

User decisions (2026-11-10): an own package WP-11 right after `!bosscooldown`
(1.5.0), before WP-09; Velopack („Ja, mit Velopack“); in addition to the
automatic check a check by hand („manuellem Update-Check zusätzlich zum
automatischen“); the proposals below as planned („Deine Empfehlungen passen
so“).

## Decision

- **Velopack** (`Velopack` package, `vpk` as a local dotnet tool, both from
  nuget.org, same version) packs every release: `Setup.exe`, `Portable.zip`,
  a full package, a delta package and the feed `releases.win.json`, all
  attached to the GitHub release of the tag. The old ZIP goes away.
- `vpk` runs in the read-only build job; the release job still only uploads
  (`gh release upload`) and runs no code from the repository or its packages.
- The installed app reads the releases through `GithubSource` (public
  repository, no token). `PokeArena:Update:Source` can point to a local folder
  with `vpk pack` output, only to try an update without a release.
- **packId `PokeArena.App`**, not `PokeArena`: Velopack installs to
  `%LOCALAPPDATA%\<packId>` and deletes that folder on uninstall, and
  `%LOCALAPPDATA%\PokeArena` is the data directory.
- An own `Main` (`Program.cs`) runs `VelopackApp.Build().Run()` before
  anything else, since Velopack starts the program with special arguments
  during install, update and uninstall.
- **When** (`UpdateCheck`, `UpdateGate` in Core, behind `IAppUpdater`):
  first check 5 minutes after the start (after a restart during a stream
  Twitch reports the stream live within seconds), then hourly; download at
  once; install automatically only while the bot takes no commands (stream
  offline and „Auch ohne Stream“ off), no dry run is active and no boss lobby
  is open, checked every minute. „Jetzt aktualisieren“ installs at once, also
  during a stream, but not while a boss lobby is open. „Nach Updates suchen“
  checks at once.
- The install hands the downloaded version to Velopack's updater (no dialogs,
  restart with the same arguments) and closes the main window, so the normal
  shutdown flushes chat queue, uploads and log.
- A copy not installed by Velopack (the old ZIP, a developer build) keeps the
  WP-08 hint: it learns from the GitHub API that a newer version exists and
  links the download.

## Alternatives

- **Own updater** (download the ZIP, check its digest, swap the folder, keep
  the old one for rollback): no new dependency and an easy rollback, but the
  file swap, locked files and the restart would be own, hard-to-test code.
  Rejected by the user.
- **Push from GitHub Actions** (self-hosted runner or remote shell through the
  Cloudflare Tunnel): a self-hosted runner on a public repository runs
  foreign code, and the WPF app lives in a signed-in desktop session that a
  remote shell cannot restart cleanly. Rejected.
- **MSIX / ClickOnce:** a signing certificate, or dated tooling. Rejected.

## Consequences

- Tagging a release updates the running bot within about an hour after the
  next stream ends — tag only after the live test, as before. A tag set by
  mistake goes live the same way.
- One manual step for 1.6.0: install it once with `Setup.exe` (or unpack
  `Portable.zip`) **as the same Windows user** — `%LOCALAPPDATA%` and the
  Credential Manager belong to one user — and delete the old ZIP folder.
- Nothing in the program folder may be edited by hand: every update replaces
  it. Settings belong in the „Einstellungen“ card (`settings.json`).
- No automatic rollback: a version that fails to start shows the start
  message box and logs the error; the backup from before its migration stays.
  Rollback is in the backlog.
- `Setup.exe` is unsigned; Windows SmartScreen asks once before it runs.
- Version 2 (roadmap): a separate Windows service for the website would not be
  updated by Velopack, which is for apps of a signed-in user.
