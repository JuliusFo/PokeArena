# WP-11 — Automatic update of the desktop app (2026-11-10, branch wp/11-auto-update)

- Built: Velopack self-update (ADR-0010). Core: `IAppUpdater`,
  `GitHubReleaseHint` (the WP-08 hint, now behind the interface),
  `UpdateGate` (bot takes no commands, no dry run, no boss lobby —
  `BossEncounter.HasOpenLobby`), `UpdateCheck` rebuilt as a loop: first check
  after 5 min, then hourly, download at once, install when the gate allows
  (checked every minute), `CheckNowAsync` / `InstallNow` for the buttons, a
  failed check keeps an earlier download. Desktop: own `Program.Main`
  (`VelopackApp.Build().Run()` first, App.xaml as `Page`), `VelopackUpdater`
  (`GithubSource` or a local folder via `PokeArena:Update:Source`, install =
  `WaitExitThenApplyUpdates` silent with the same arguments + closing the main
  window). UI: `UpdateStatus` in the top bar. `release.yml`: `vpk pack`
  (packId `PokeArena.App`) after the publish, `vpk download github` before it
  for the delta, only this version's files on the release, Setup and Portable
  renamed to `PokeArena-vX.Y.Z-…`. README „Installing and running“, release
  skill, architecture, Desktop pitfalls. No „Neuigkeiten“ line (players do not
  notice).
- User decisions: own package before version 2 and right after
  `!bosscooldown`, first as 1.6.0; at the merge „Jetzt mergen, beides 1.5.0“
  (WP-11 ships in 1.5.0 with `!bosscooldown`, WP-09 stays 1.6.0); „Ja, mit
  Velopack, manuellem Update-Check
  zusätzlich zum automatischen. Deine Empfehlungen passen so.“ (Setup.exe
  recommended, Portable.zip as well, no off switch, no rollback, hourly).
- Addenda: checked locally with two packed test versions (packId
  `PokeArena.AppTest`, Portable, local source, scratch data, no channel): the
  9.0.0 copy restarted as 9.0.1 by itself after the first check, with its
  arguments, in about 4 s, no warnings in the log (before the review's
  10-minute wait).
- Pitfalls: the packId must not be `PokeArena` (the uninstall deletes
  `%LOCALAPPDATA%\<packId>`); the updater waits only 60 s for the process to
  end; .NET 10's `BackgroundService` starts `ExecuteAsync` on the thread pool,
  so timer tests advance the `FakeTimeProvider` a minute at a time until the
  loop has reached the state (`PumpUntilAsync`).
- Deliberately not done (→ backlog): automatic rollback after a failed start,
  a switch to turn updates off, signing `Setup.exe`.
- Code review (2026-11-10): „Alle wie empfohlen“; second pass „Ja, alle drei“.
  - M Velopack installs a downloaded update on the next start by default (mid-stream after a crash or settings
    restart) → fixed, `SetAutoApplyOnStartup(false)` + Desktop pitfall.
  - L the uploaded feed listed the previous release's full package → fixed, feed filtered to this version (snippet
    checked on a real vpk feed); `assets`/`RELEASES` no longer uploaded.
  - L feed and packages uploaded in one call; a full re-run would fail on the existing version → fixed, feed uploaded
    last; release skill says "re-run failed jobs only".
  - L `.btn-small` overridden by `.btn` → fixed (`.btn.btn-small`).
  - L the lobby looked closed while its fight was resolved → fixed, `_resolving` flag; test via `RecordingSender.OnSend`.
  - L update source matched loosely → fixed: exact repository or an existing local non-UNC folder (second pass:
    `//server/share` too).
  - L a short stream drop opened the gate → fixed, 10 minutes of idling (user: „wie empfohlen“); second pass: the dry
    run switch starts the wait again.
  - L stale comments → fixed. L missing tests (busy, failed newer download) → added.
  - Found while fixing: `vpk pack` defaulted to x86 → `--runtime win-x64`.
  - Second pass L tooltip and README out of date → fixed.
  - The old tooltip „Lade das ZIP herunter …“ is gone with the merge into 1.5.0; a 1.4.2 copy still shows it, so the switch follows README „Switching from the ZIP“.
