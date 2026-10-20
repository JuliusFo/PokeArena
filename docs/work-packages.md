# Work packages

Overview and a log entry per finished package. Plans and open questions live
in [roadmap.md](roadmap.md).

| WP | Name | Version | Status |
|---|---|---|---|
| WP-00 | Scaffold | 0.1.0 | done 2026-10-11 |
| WP-01 | Twitch connection | 0.1.0 | done 2026-10-13 |
| WP-02 | Domain foundation | 0.2.0 | done 2026-10-13 |
| WP-03 | Battle engine + boss | 0.3.0 | done 2026-10-19 |
| WP-04 | PvP | 0.4.0 | done 2026-10-20 |
| WP-05 | NPC fights | 0.5.0 | planned |
| WP-06 | Website | 0.6.0 | planned |
| WP-07 | Operations | 1.0.0 | planned |

## Log

### WP-00 — Scaffold (2026-10-11, branch wp/00-scaffold)
- Built: `PokeArena.slnx` with Core, UI (Razor class library), Desktop (WPF host
  with `BlazorWebView`), Core.Tests and Integration.Tests; shared build
  settings (warnings as errors, central package versions, `global.json`,
  `nuget.config`, `.gitattributes`, `.editorconfig`); Generic Host, Serilog
  file logs, global exception handlers, single-instance guard, window chrome
  following the Windows theme; `AppPaths`/`PokeArenaOptions`
  (`--PokeArena:DataDirectory`); `ISecretStore` + Credential Manager
  implementation; banned-API analyzer for clock/randomness in Core; CI on
  `windows-latest`. App starts with an empty welcome shell; 6 unit and
  3 integration tests green locally.
- User decisions: xUnit v3; Meziantou Credential Manager package; Blazor
  Hybrid instead of WPF-UI (ADR-0007); `nuget.config` limited to nuget.org
  instead of "no NuGet.config" (ADR-0001 addendum).
- Addenda: ADR-0001 and ADR-0004 (analyzer enforcement; seeded `Random`
  allowed).
- Pitfalls: a global DevExpress feed on the dev machine broke the build with
  NU1507 under central package management → `nuget.config` with `<clear />`.
  `core.autocrlf=true` on the dev machine still prints "LF will be replaced by
  CRLF" on `git add` — harmless, the repository stores LF.
- Deliberately not done: Web project (WP-06), bUnit/UI test project (with the
  first components that have logic, WP-01), release workflow (WP-07), app icon.

### WP-01 — Twitch connection (2026-10-13, branch wp/01-twitch-connection)
- Built: own lean Twitch client in Core — device-code login for a public
  client, token provider with serialized refresh (single-use refresh tokens)
  and validation incl. scope check, Helix client (users, chat subscription,
  send), EventSub WebSocket session with keepalive timeout, `session_reconnect`
  handover and duplicate filter; `TwitchConnection` with automatic reconnect
  (2 s … 60 s), hourly validation and UI state; `ChatInbox` as the single
  serialized command queue, `CommandProcessor`, `ChatSender` (20 per 30 s,
  1 per second, 500-character split), `ChatLog`, dry run; `!pokearena` with a
  30 s cooldown (not in the dry run). Desktop: Twitch config, optional
  `appsettings.local.json`. UI: connection, dry-run and chat-log panels.
  Tests: 56 unit (fake HTTP, fake sockets, `FakeTimeProvider`), 7 bUnit, one
  skippable Twitch integration test; new `PokeArena.UI.Tests` in CI.
- User decisions: own client instead of TwitchLib, dry run, `!pokearena` as
  permanent status command (ADR-0002 addendum 2026-10-12); Twitch app
  registered as public client; test channel `ShiyoFurball`; client ID
  committed (public by design).
- Addenda: ADR-0002 (2026-10-12 decisions; 2026-10-13 grants, send rate, no
  token expiry in the UI, shared chat, channel setting). `AppInfo` moved to
  Core.
- Pitfalls: see `src/PokeArena.Core/CLAUDE.md` (parallel refresh logs the bot
  out; cancelling a WebSocket receive aborts the socket; shared chat; U+E0000
  suffix). Raw JSON in C# tests: `}}}` needs `$$$` raw strings or spaces.
- Live test 2026-10-13: bot account signed in via device code, `!pokearena`
  answered in `ShiyoFurball`; Twitch integration test green against the real
  API.
- Deliberately not done: detecting moderator status for a higher send rate,
  persisting the dry-run toggle (no DB before WP-02), channel setting in the
  UI (WP-07 setup question).

### WP-02 — Domain foundation (2026-10-13, branch wp/02-domain-foundation)
- Built: EF Core + SQLite model `Trainer` / `Specimen` (UTC-tick timestamps,
  enums as text, filtered unique index "one starter per trainer"), initial
  migration, `dotnet-ef` as local tool; live database
  `%LOCALAPPDATA%\PokeArena\pokearena.db` in WAL mode, migrated at startup;
  `SpeciesCatalog` loads and validates `data/pokemon.json` (shipped with the
  app); `GameDatabase` switches between live database and an in-memory dry-run
  sandbox; `TrainerService` (registration in one transaction, display-name
  refresh with cache, trainer list); `!registrieren`; `CommandAccess.Registered`
  with a hint for unregistered users; the processor drops real messages during
  the dry run and simulated ones outside it. UI: Bot/Trainer tabs, read-only
  trainer list with search, "Probelauf aktiv" badge. Tests: 80 unit (in-memory
  SQLite, real base data), 12 bUnit.
- Live test 2026-10-13: `!registrieren` answered in `ShiyoFurball`, the
  registration survived a restart, the dry run kept its test trainers apart.
- User decisions: the dry run gets its own sandbox database that starts empty
  each time; real messages are not processed meanwhile.
- Addenda: none (EF Core + SQLite already in ADR-0001). `DryRunMode` moved to
  the Core root namespace; Microsoft packages aligned to 10.0.13 (EF Core 10.0.13
  pulled a newer `Logging.Abstractions`, NU1605 under warnings as errors).
- Pitfalls: see `src/PokeArena.Core/CLAUDE.md` (contexts only via
  `GameDatabase.Open()`, UTC ticks, migration command, caches reset on
  `Switched`). `dotnet new tool-manifest` on SDK 10 writes `dotnet-tools.json`
  to the repo root, not `.config/`.
- Deliberately not done: backup before migrations (WP-07), editing or
  deleting trainers (WP-07), balancing values in the database (WP-03),
  throttling repeated hints (backlog).

### WP-03 — Battle engine + boss (2026-10-15, branch wp/03-battle-engine-boss)
- Built: `TypeChart` (`data/typen.json`) and `NpcCatalog` (new `data/npcs.json`,
  17 NPCs with threshold, team as slugs, strength 1.0) validated at startup;
  all `data/*.json` shipped with app and tests. Engine in `Game/Battles/`:
  `DamageCalculator` (ADR-0006 formula, round half away from zero, minimum 1),
  `BossBattle`, `TeamBattle` (1-vs-1 with teams for WP-04/05), `BossDraw`,
  structured `BattleLog` (JSON in the DB, German lines rendered),
  `BattleSettings`/`BossSettings` with the spec defaults, `ISeedSource` +
  `BattleRandom`. `ChatInbox` carries `QueuedWork`; `BossEncounter` with lobby,
  countdown, fight, catches, achievements and cooldown in one transaction;
  `!boss`. Tables `Battles`, `BattleParticipants`, `AchievementUnlocks`,
  `Cooldowns` (migration `BossEncounters`). UI: "Kämpfe" area with list and
  log. `tools/BalancingSimulator.cs`. Tests: 108 unit, 15 bUnit.
- User decisions: plan agreed 2026-10-15 (boss flow as in spec §4), with it
  the four proposals: code defaults for balancing, no cooldowns in the dry run,
  throttled cooldown reply, no battle link before WP-06. `Skalierung` 20
  instead of 25 (2026-10-19).
- Addenda: ADR-0006 (simulator results 2026-10-15; `Skalierung` 20 on
  2026-10-19); spec
  §4 (reply drafts, cooldown reply, dry run) and §13 (code defaults for now).
- Pitfalls: see `src/PokeArena.Core/CLAUDE.md` (timed steps via `QueuedWork`,
  draw order per seed, `PublishAot=false` for file-based tools). In PowerShell
  scripts `R` is an alias of `Invoke-History` — don't name helpers `R`.
- Live test 2026-10-19: dry run with four simulated viewers — registration,
  too-few lobby, boss fights won and lost, catches, "Kämpfe" view, switching
  back to the live database. User decision: no separate channel test.
- Deliberately not done: runtime balancing editor (roadmap question),
  battle link in chat (WP-06), publishing (WP-06), `!bossreset` (WP-07),
  distinct-species column in the trainer list (not planned).

### WP-04 — PvP (2026-10-19, branch wp/04-pvp)
- Built: `Trainer.Login` (refreshed with the display name, migration
  `TrainerLogin` pre-fills the lower-cased display name) and
  `TrainerService.FindByNameAsync` (login first, then display name);
  `PvpService` + `!fight` — challenge in memory (expires lazily after
  2 minutes, dropped on a database switch via a generation counter), accept
  with the same command, requirements checked again, one seed for both teams
  of 6, hits and loot, `TeamBattle`, loot transfer (never the starter) and
  the winner's achievements in one transaction; `PvpSettings`;
  `BattleKind.Pvp`, outcomes `ChallengerWon` / `OpponentWon`,
  `Battle.LootSpecimenId` / `LootSpeciesId` (migration `PvpBattles`). UI:
  PvP rows in "Kämpfe" („Ash gegen Misty“, winner and loot); dry-run button
  „Mit 6 Pokémon ausstatten“ (`DryRunTrainers`, runs on the command queue,
  sandbox only). Tests: 133 unit, 18 bUnit.
- User decisions: plan agreed 2026-10-19 with all five proposals (login
  column, in-memory challenge, DB columns, reply drafts, PvP rows in the
  battle list); 2026-10-20: a dry-run button that tops a test viewer up to
  six Pokémon, because reaching six through boss fights made testing slow.
- Addenda: spec §6 (mention matching, in-memory challenges, achievements by
  loot, reply drafts) and §10 (open challenges in memory); architecture (data
  model, PvP flow, no `OpenChallenge` table).
- Pitfalls: see `src/PokeArena.Core/CLAUDE.md` (database switch fires on the
  UI thread → generation counter; mentions by login) and
  `src/PokeArena.UI/CLAUDE.md` (`@@` in Razor markup). The result reads „…
  bekommt Karpador von Misty“ instead of the planned „dessen Karpador“, which
  assumes a gender.
- Live test 2026-10-20: dry run — two test viewers equipped with the new
  button, challenge and accept, fight resolved with loot transfer. User
  decision: no separate channel test.
- Deliberately not done: pair cooldown (spec §6), withdrawing or declining a
  challenge, the PDF's pick-your-team mode, battle link (WP-06).

Template per entry:

```
### WP-NN — Name (YYYY-MM-DD, branch wp/NN-short-name)
- Built:
- User decisions:
- Addenda:
- Pitfalls:
- Deliberately not done (→ backlog):
```
