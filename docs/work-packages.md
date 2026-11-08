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
| WP-05 | NPC fights | 0.5.0 | done 2026-10-27 |
| WP-06 | Website | 0.6.0 | done 2026-11-01 |
| WP-07 | Live status | 0.7.0 | done 2026-11-03 |
| WP-08 | Operations | 1.0.0 | done 2026-11-04 |
| R1 | Review 1.0.3 fixes ([review-1.0.3.md](review-1.0.3.md)) | – | built 2026-11-08, live test open |
| WP-09 | Balancing after the first streams | 1.1.0 | planned |

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
  first components that have logic, WP-01), release workflow (WP-08), app icon.

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
  UI (WP-08 setup question).

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
- Deliberately not done: backup before migrations (WP-08), editing or
  deleting trainers (WP-08), balancing values in the database (WP-03),
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
  battle link in chat (WP-06), publishing (WP-06), `!bossreset` (WP-08),
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

### WP-05 — NPC fights (2026-10-21, branch wp/05-npc-fights)
- Built: `NpcService` + `!challenge <NPC>` (name or id, case-insensitive) —
  unlock check against `AchievementUnlock`, cooldown per trainer and NPC in
  the `Cooldown` table (`npc:<trainer>:<npc>`, 48 h after a loss, 60 min
  after a win, skipped in the dry run), team size check, one seed for the
  trainer's team of 6, the hits and the reward specimen, `TeamBattle` against
  the fixed team × strength; battle, tokens, badge and reward specimen, the
  achievements it reaches and the cooldown in one transaction. `NpcSettings`;
  `npcs.json` field `tokens` (Eich 1 … Ash 17); `BattleKind.Npc`,
  `Battle.NpcId` / `Tokens`, `Trainer.Tokens`, `NpcBadge`,
  `SpecimenOrigin.Npc` (migration `NpcFights`). UI: NPC rows in "Kämpfe"
  („Ash gegen Eich“, reward or „Eich gewinnt“), columns „Orden“ and „Tokens“
  in the trainer list, dry-run NPC picker „Freischalten“ that tops a test
  viewer up to the NPC's threshold and unlocks the achievements. Tests:
  150 unit, 23 bUnit.
- User decisions: E22 on 2026-10-21 — only the first win brings a reward:
  badge, tokens staggered per NPC in `npcs.json` and a team specimen the
  trainer lacks (first built as tokens for every win, corrected the same
  day); redeeming tokens later. Plan agreed with the proposals (rising token
  amounts, cooldowns kept, plain balance, reply drafts).
- Addenda: spec §1 (badge, tokens), §7 (reward, dry run, reply drafts), §10,
  §14 (E22 closed); architecture (data model, NPC flow); backlog (token
  redemption, ledger, balance command).
- Pitfalls: none new.
- Live test 2026-10-27: dry run — test viewer unlocked for Eich via the NPC
  picker, ten fights against Eich, all won: the first with badge, Hornliu and
  1 token, the other nine without reward. A loss did not come up in the dry
  run (covered by unit tests). Eich looks easy; user decision: leave it for
  now (roadmap question).
- Deliberately not done: redeeming tokens, a token or badge command in chat,
  badges on the website (WP-06).

### WP-06 — Website (2026-11-01, branch wp/06-website)
- Built: `PokeArena.Contracts` (site records, source-generated JSON,
  ADR-0009). Core `Publishing/`: `SiteOutbox` queues paths in the game
  transaction (registration, rename, boss, PvP, NPC), `SiteGenerator` renders
  `meta`, `rangliste`, `kaempfe`, `trainer/<id>`, `kampf/<id>` from the DB,
  `SitePublishWorker` uploads from the live outbox (2 s poll, backoff 5 s …
  15 min, never gives up, sandbox never published, deletes files whose source
  is gone), `R2SitePublisher` / `R2Access` (AWSSDK.S3, key in the Credential
  Manager), `SiteLinks` + `!arena`; migration `Website` (`PublishQueue`,
  `Specimen.AcquiredAt` with backfill from PvP loot). Desktop: „Website“ and
  „OBS-Overlay“ cards, `OverlayServer` (Kestrel on loopback, port 5299).
  `PokeArena.Web` (Blazor WASM): leaderboard, battle index, battle page with
  404 retry, trainer page by id or name, privacy note, fan disclaimer;
  invented sample data for local runs. Workflow `pages.yml` (Pages deploy,
  `DATA_URL` variable, 404.html fallback); CI runs the website tests. Tests:
  171 unit, 26 bUnit (desktop), 9 bUnit (website), integration: overlay
  server, R2 round trip (skipped until set up).
- User decisions: 2026-10-30 — no `r2.dev` (unbounded per-request cost);
  bucket in the user's Cloudflare account behind a subdomain of the existing
  zone (HorizonNET) with cache and a rate-limiting rule; the site gets a
  subdomain too (ADR-0003 addendum). 2026-11-01 — plan agreed with the
  proposals: shared contracts project, republish-all already in WP-06,
  overlay = latest fight + top 5, fan disclaimer in the footer, tie-break by
  `AcquiredAt`.
- Addenda: ADR-0003 addendum, ADR-0009, spec §9 (custom domain, tie-break
  rule), architecture (publishing, overlay, website), new
  docs/website-setup.md, backlog (custom-domain item closed).
- Pitfalls: Core (queue paths in the same transaction, `OpenLive`,
  `BattleKind` clash), Desktop (System.IO, overlay port and log level), new
  Web/CLAUDE.md.
- Deviation from the architecture sketch: the outbox stores only paths, no
  payload — files are rendered at upload time, so they are always current
  and duplicates collapse.
- Live test 2026-11-01: Cloudflare (bucket behind `arena-data.cloudfox.xyz`,
  cache rule, rate-limiting rule by path — the free plan has no hostname
  field, CORS, account API token) and Pages (`arena.cloudfox.xyz`, verified
  domain, `DATA_URL`) set up by the user. Key entered in the app, „Alles neu
  veröffentlichen“ uploaded the existing data; second request a cache HIT,
  404s not cached (BYPASS), CORS header and `Vary: Origin` present; R2
  integration test green. A real `!boss` in the test channel (two trainers,
  boss escaped) posted the battle link, and `kampf/1.json`, `kaempfe.json`
  and `rangliste.json` were on the data host about a second later. Website
  run locally against the real data host; the Pages deploy follows the merge.
- Deliberately not done: deleting trainers and „[gelöscht]“ in old fights
  (WP-08; the privacy page already describes it), purging cached fight files
  after a deletion (WP-08), overlay content beyond the simplest variant.

### WP-07 — Live status (2026-11-03, branch wp/07-live-status)
- Built: `StreamStatus` (live state + in-memory „Auch ohne Stream“ switch);
  `TwitchConnection` subscribes to `stream.online` / `stream.offline` next to
  the chat and asks Helix `GET /streams` after subscribing and with the hourly
  token check; changes go to the chat log. `CommandProcessor` drops real
  messages while offline; known commands get one hint per 5 minutes, the dry
  run is never gated. Twitch card shows „Stream: live / offline“ and the
  switch. 5 new tests (connection, gate, card); 218 tests green.
- User decisions: own package before operations (now WP-08, versions shift:
  0.7.0 here, 1.0.0 stays operations); offline hint with a global cooldown;
  the switch is not stored.
- Pitfalls: Core (gate in `CommandProcessor` tests, ask the live state only
  after subscribing).
- Live test 2026-11-03 passed (user, test channel): go live / end stream,
  offline hint with cooldown, „Auch ohne Stream“ switch. Released as 0.7.0.

### WP-08 — Operations (2026-11-04, branch wp/08-operations)
- Built: mod commands bypass the live gate; `!bossreset`; trainer deletion
  (`TrainerService.DeleteAsync` in one transaction, `TrainerRemoval` with the
  same-moderator confirmation for `!entfernen`, inline-confirmed button in the
  trainer list) that overwrites name and id in stored battle logs and queues
  trainer page, lists, fights and co-participants' pages; boss catchers on the
  site read from the log; fight files `max-age=86400`; `DatabaseBackup`
  (`VACUUM INTO` before the migrations on every start and „Jetzt sichern“,
  newest 10); `settings.json` in the data directory as configuration layer
  with the „Einstellungen“ card; `release.yml` (self-contained win-x64 ZIP,
  notes from the CHANGELOG); `UpdateCheck` hint in the top bar; README for
  the streamer; `docs/chat-texts.md` and the reviewed wording. 237 tests
  green.
- User decisions: the 2026-11-04 proposals (mod commands offline, ZIP instead
  of installer, settings card, shorter fight cache instead of a purge, one
  wording pass); confirmation for `!entfernen`; names overwritten in the
  stored logs (ADR-0004 addendum); all six wording proposals accepted.
- Deviations: the backup card shows the folder path instead of an „Ordner
  öffnen“ button (the UI library stays free of Windows APIs); after saving the
  settings the card asks to close and reopen the app instead of offering a
  restart button (it would collide with the single-instance mutex).
- Pitfalls: Core (`VACUUM INTO` from an in-memory database writes into memory,
  so backup tests use a real file in Integration.Tests); Desktop (settings
  layer order; `appsettings.local.json` is a Razor SDK Content item and is
  now excluded from publish).
- Live test 2026-11-04 passed (user, test channel). Released as 1.0.0; the
  first tag also runs the release workflow for the first time.
- Deliberately not done (→ backlog): manual corrections of trainers, backups
  to a second location.

### R1 — Review 1.0.3 (2026-11-08, branch wp/r1-review-1.0.3)
- Built: the fixes of the first whole-solution review
  ([review-1.0.3.md](review-1.0.3.md), six agents on `a2fdf4f`), and the
  `/complete-code-review` skill that runs such a review. Part A: wrong-account
  login detected, only 403 stops Twitch, event wins over a lagging live check
  plus rechecks after the start, connection loop off the UI thread, ordered
  shutdown (`Closing` → `StopAsync`; sender drains its queue, waits up to 60 s
  for a reconnect), refresh never cancelled, WebSocket connect timeout, lead
  moderators; per-viewer hint throttle (`SendHint`), safe echo of typed names,
  dry-run switch on the command queue, unique logins, open challenges dropped
  on deletion, `!challenge @Eich`, base data tiers checked, no names in the
  log, registration/rename in a transaction; republish lists the bucket and
  keeps fight ids above it, fresh changes first, retry on a new R2 key;
  overlay port status, start-order hint, cached state, „1 Art“; website
  timeouts, empty states, older-format hint, dated „Stand“; release workflow
  with read-only build job and version/CHANGELOG check; banned real-time
  timers, fixed analysis level. Part B: `BattleTexts` for site and desktop,
  one `BattleKind`, `ReplyThrottle`, cooldown helpers, `ChatText.Duration`,
  `TeamBattleResult.Rounds`, simulator reads code defaults, dead code. Part C:
  gaps proven by mutation closed (see the review), architecture and asset
  tests, guide vs `npcs.json`, shared fakes. Part D: spec, architecture,
  README, roadmap, ADRs, chat texts, setup guide, checklist, pitfalls. Tests
  233 → 292 (Core 192 → 250, UI 30 → 27, Web 11 → 15), integration tests
  adjusted.
- User decisions: one package for everything; the process as a skill.
  Questions 2–7 left open → the default of each proposal (recorded in the
  review document).
- Not reproduced: A31 (the battle list is already notified on deletion).
- Code review (2026-11-08, second pass with `/branch-code-review`): „Jo, deine
  Vorschläge passen.“ — all points, hint throttle per viewer and command.
  - 1 Bucket listing unchecked → fixed: only the bot's own paths, id jump
    capped (`MaxBattleIdGap`), test with foreign and far-away files.
  - 2 Synchronous host dispose in `OnExit` → fixed: closed window, `await`
    `DisposeAsync`, then `Shutdown`; checked by hand on a scratch data
    directory (process gone after ~0.2 s, shutdown lines in the log).
  - 3 Hint throttle shared by all commands and active in the dry run → fixed:
    per viewer and command, never in the dry run; test.
  - 4 Live state reports unsynchronised on the thread pool → fixed: one lock.
  - 5 Sender waited 60 s per message and up to 5 s while closing → fixed: one
    minute in total, at most 3 s while closing; test.
  - 6 Released login, odd names in mod replies, ambiguous names → fixed:
    confirmation by display name, `ChatText.Echo`, own replies for ambiguous
    names (`docs/chat-texts.md` rows 18, 36, 37); tests.
  - 7 Not noted: A5's maximum age for queued hints → not done, the throttle per
    viewer and command bounds the queue; C11's overlay top five → test added.
  - 8 Tests for A12 and A13 → added.
  - 9 README blank line and „republish before the next fight“ → fixed.
  - Pre-existing: tag name pasted into release scripts → fixed (env); fight
    page retry loop on an id change → backlog.
  - Re-check of the fixes: all nine confirmed; two new small points, the
    user's decision „Ja, beide“: (10) the shared wait start of fix 5 made a
    later fight result drop at once → each queued message now waits until one
    minute after it was queued; test. (11) Desktop pitfall still described the
    old shutdown → updated; disposal capped at 5 s, checked by hand again.
- Not done: C22 (a theory would mix the reason test with the backoff test);
  RD5's NPC team size from `PvpSettings` (spec: one team size for all team
  fights); folder moves of test files beyond the shared fakes.
- Pitfalls: Core (hints via `SendHint`, Twitch errors and live check, banned
  timer overloads, dry-run switch on the queue); Desktop (no `await` in
  `OnExit`).
- Deliberately not done (→ backlog): local overlay file (A8), reading the
  previous data format (A33), connection test in the settings card.

Template per entry:

```
### WP-NN — Name (YYYY-MM-DD, branch wp/NN-short-name)
- Built:
- User decisions:
- Addenda:
- Pitfalls:
- Deliberately not done (→ backlog):
```
