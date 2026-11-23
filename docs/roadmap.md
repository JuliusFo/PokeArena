# Roadmap

Work packages up to v1.0.0, the packages and branches after it (all released
up to 1.10.0; the index is docs/work-packages.md), WP-09 as the next planned
one (1.11.0, proposal) and a parked idea for version 2. Read the package before starting it and agree the plan with
the user. Estimates: solo, "day" = one focused person-day; total ≈ 25–31
days. Every package ends in a state that could run on stream.

Order is deliberate. `data/pokemon.json` exists since 2026-10-11 (ADR-0005),
so WP-03 is no longer blocked by base data.

## WP-00 — Scaffold (≈ 1 day) → v0.1.0

**Done 2026-10-11** — see the log in work-packages.md.

## WP-01 — Twitch connection (4–5 days) → v0.1.0

Device-code auth, EventSub read, Helix send, rate limiter; desktop shell
(Blazor, ADR-0007) with connect button, status, live chat log, dry run.
Demo: the bot answers `!pokearena` in the channel.

- User decision (2026-10-11): the user sets up the bot account and tokens.
- User decisions (2026-10-12): own lean Twitch client instead of TwitchLib
  (ADR-0002 addendum); a minimal dry run ("Probelauf") — simulated viewers
  type commands in the UI, replies go only to the log; `!pokearena` as
  permanent status command (spec §8). Estimate grows to 4–5 days.
- Setup (2026-10-12): bot account exists; Twitch app registered as Public
  client, client ID in `appsettings.json`. Test channel during development:
  the user's own channel `ShiyoFurball`.
- Answered by the docs (ADR-0002 addendum 2026-10-13): with a user token over
  WebSocket neither `channel:bot` nor moderator status is required (live
  test ran with the bot as moderator; without it untested).
- **Done 2026-10-13**: live test passed — bot account signed in via device
  code, `!pokearena` answered in the test channel, integration test green
  against the real API. See the log in work-packages.md.
- Proposal (review 2026-10-11, accepted for WP-01): process all chat commands
  on one serialized queue (`Channel<T>`) — battles resolve instantly, so this
  removes race conditions at no cost.
- Pitfalls: see `src/PokeArena.Core/CLAUDE.md` (keepalive, `session_reconnect`,
  single-use refresh tokens).
- Check the current send-rate limits for bots and moderators against the
  Twitch docs before implementing the rate limiter.
- First components with logic → add a UI test project with bUnit.

## WP-02 — Domain foundation (2–3 days) → v0.2.0

EF Core model + migrations, base-data loader, `!registrieren`, trainer list
in the UI.
Demo: viewers can register; data survives a restart.

- Plan (agreed 2026-10-13, ≈ 3 days): only `Trainer` and `Specimen` tables;
  `dotnet-ef` as local tool; `SpeciesCatalog` validates `data/pokemon.json` at
  startup; "registered only" command access with a hint for unregistered
  users (spec §3); display-name refresh; "Bot" / "Trainer" navigation with a
  read-only trainer list.
- User decision (2026-10-13): the dry run gets its own sandbox database that
  starts empty each time it is switched on. While it is on, real chat
  messages are only logged, not processed — real player data stays untouched.
- **Done 2026-10-13**: live test passed — registration in the test channel,
  data survived a restart, dry run isolated. See the log in work-packages.md.

## WP-03 — Battle engine + boss (4–5 days) → v0.3.0

Damage, types, crits, seed, log; boss lobby, rarity draw, cooldowns,
achievements.
Demo: `!boss` playable end-to-end.

- User decisions (2026-10-11, ADR-0005): official Gen-1 values, real dual
  types (multiplied on defence, better type on attack).
- User decision (2026-10-11): generated rarity list accepted as is
  (98 common / 48 rare / 5 legendary).
- User decisions (2026-10-11, ADR-0006): damped damage ratio with best of
  physical/special (`k` = 50); boss attacks `⌈N/3⌉` targets, HP =
  base HP × 1.8 × N; target group win rate ≈ 75 % (targets and HP changed
  in 1.8.0, see WP-09).
- Every species has a rarity tier, so all 151 can appear as boss — the
  achievements 150/151 are reachable.
- Build a Monte Carlo balancing simulator (≈ 0.5 day) on the real engine:
  first reproduce the ADR-0006 numbers, then fine-tune `Scaling`, `k` and
  the first-strike advantage (18 % one-hit KOs in 1-vs-1).
- Plan (agreed 2026-10-15): `typen.json` and new `npcs.json` loaded and
  validated at startup; one seed per encounter; structured battle log as JSON,
  German lines rendered from it; the lobby countdown posts its close into the
  command queue; read-only "Kämpfe" area. Proposals taken over with the plan:
  balancing values as code defaults for now (runtime editor later), no boss
  cooldowns in the dry run, cooldown reply „der nächste Boss kann in X Minuten
  gerufen werden" at most every 30 s, no battle link in chat until WP-06.
- **Done 2026-10-19**: built 2026-10-15; dry run with simulated viewers passed,
  the user waived the separate channel test. See the log in work-packages.md.
- User decision (2026-10-19, ADR-0006 addendum): `Scaling` 20 instead of
  25 (13 % one-hit KOs, first strike wins 57.5 %); `k` stays 50.
- Closed: the `N = 4` dip (≈ 64 % group wins vs. ≈ 75 % target) — gone with
  the boss balancing of 1.8.0 (`feat/boss-balancing`, see WP-09 below).
- Runtime balancing editor: user decision (2026-11-04) — not in v1; values
  stay code defaults and change by release (spec §13 adjusted). See WP-09.
- Open: progression length. Expected catches for k distinct species (uniform
  lower bound) — 15: ~16, 60: ~76, 100: ~163, 151: ~845; with ≈ 60 % catch
  chance per boss that is ≈ 1.7× as many boss fights. → WP-09 (user decision
  2026-11-04).

## WP-04 — PvP (3–4 days) → v0.4.0

Challenge/accept dialog, timeout, random teams, loot transfer.
Demo: `!fight` playable.

- Spec gap: achievement unlocks caused by PvP loot must be announced too.
- Plan (agreed 2026-10-19, ≈ 3 days): one seed per fight draws both teams
  (challenger first), all hits and the loot; fight, loot transfer and the
  winner's achievements in one transaction (closes the spec gap above);
  requirements re-checked on accept. Proposals taken over with the plan:
  `Trainer.Login` (refreshed like the display name, migration pre-fills the
  lower-cased display name) so `@name` resolves by login, falling back to the
  display name; the open challenge lives in memory like the boss lobby,
  expires lazily without a timer (spec: silently) and is dropped when the dry
  run is switched — no `OpenChallenge` table; `BattleKind.Pvp`, outcomes
  `ChallengerWon` / `OpponentWon`, `Battle.LootSpecimenId` /
  `LootSpeciesId`, two participant rows; specimen origin and catch time stay
  unchanged on transfer; drafted replies for challenge, result and
  rejections, no battle log or link in chat; PvP rows in the "Kämpfe" list.
  Not included: pair cooldown (spec §6), withdrawing or declining a
  challenge, the PDF's pick-your-team mode, battle link (WP-06).
- User decision (2026-10-20): the dry run gets a button that tops a test
  viewer up to a full team, so `!fight` can be tested without boss fights.
- **Done 2026-10-20**: built 2026-10-19; dry run with test viewers passed, the
  user waived the separate channel test. See the log in work-packages.md.

## WP-05 — NPC fights (2–3 days) → v0.5.0

NPC fights, reward, unlock announcements.
Demo: `!challenge Eich` playable.

- User decisions (2026-10-21, E22): only the first win against an NPC brings
  a reward — a badge, the NPC's tokens (staggered per NPC in `npcs.json`,
  Eich 1 … Ash 17) and one specimen of the NPC's team, preferring species the
  trainer lacks. (Briefly decided as tokens for every win, corrected the same
  day.) Both cooldowns stay as specified. Redeeming tokens is not part of
  WP-05 (backlog).
- User decision (2026-11-19, streamer feedback): every NPC gives 1 token for
  the first win, no staggering any more (`npcs.json`, → 1.8.0). Balances
  already earned stay as they are.
  Open question: should trainers who already beat a higher NPC keep the extra
  tokens (current behaviour) or be corrected down? Matters only once tokens
  can be redeemed.
- Plan (agreed 2026-10-21, ≈ 3 days): cooldowns in the `Cooldown` table
  (key `npc:<trainer>:<npc>`); `BattleKind.Npc` with `Battle.NpcId`; new
  `NpcBadge` table (first win, permanent); `Trainer.Tokens` as a plain
  balance; one seed draws the player team, the hits and the reward specimen;
  fight, reward, achievements and cooldown in one transaction; NPC rows in
  "Kämpfe", tokens and badges in the trainer list; the dry run can top a test
  viewer up to an NPC's threshold. Not included: redeeming tokens, a token
  command in chat, badges on the website (WP-06).
- **Done 2026-10-27**: built 2026-10-21; dry run passed. See the log in
  work-packages.md.
- Open: NPC difficulty. In the dry run a test viewer with 15 random distinct
  species beat Eich (strength 1.0, basic forms only) 10 times out of 10. User
  decision (2026-10-27): leave it for now; if NPCs feel too easy on stream,
  raise `staerke` per NPC in `npcs.json` or measure win rates with the
  simulator. → WP-09 (user decision 2026-11-04).

## WP-06 — Website (4–5 days) → v0.6.0

Blazor pages (leaderboard, battle, trainer, overlay), GitHub Actions deploy of
the app, R2 bucket + CORS + cache headers, R2 publisher + outbox, local
overlay endpoint.
Demo: a battle link in chat opens a live page.

- User decisions (2026-10-30/11-01, ADR-0003 addendum): the bucket lives in
  the user's Cloudflare account, behind a subdomain of the user's existing
  zone with cache and one rate-limiting rule; `r2.dev` stays off (a flood of
  uncached reads is billed per request with no spending cap). The site gets a
  subdomain of the same zone. Both are first-level subdomains (Universal SSL).
- User decision (2026-11-01, ADR-0009): the site JSON DTOs live in a
  dependency-free `PokeArena.Contracts` project referenced by Core and Web.
- Plan (agreed 2026-11-01, ≈ 5 days): outbox entries written in the same
  transaction as the fight; dry run never publishes; R2 credentials entered in
  the desktop app (Credential Manager) with a publish status; battle links in
  chat and `!arena`; pages leaderboard, battle, trainer, battle index, privacy
  note; Pages deploy workflow; local overlay endpoint. Proposals taken over
  with the plan: "republish everything" already in WP-06 (otherwise data from
  0.5 never appears); the overlay shows the latest fight and the top 5 (the
  spec leaves it open); the footer carries the fan disclaimer (ADR-0008);
  leaderboard ties go by `Specimen.AcquiredAt` — the moment the trainer last
  completed the set of species they own now (losing and regaining one moves
  that moment).
- **Done 2026-11-01**: built and live-tested the same day (Cloudflare and Pages
  set up by the user, real boss fight in the test channel). See the log in
  work-packages.md.
- Overlay content beyond the simplest variant: user decision (2026-11-04) —
  wait for the streamer's feedback; the next candidate (live boss countdown)
  is parked in backlog.md.

## WP-07 — Live status (≈ 1 day) → v0.7.0

The bot takes chat commands only while the channel is live.
Demo: commands are answered only after going live.

- User decision (2026-11-03): its own package before operations (which moves
  to WP-08). Offline, a known command gets a short hint with a global
  cooldown; unknown commands stay silent (they may belong to another bot). An
  "always active" switch in the desktop app lets the streamer test offline;
  it is not stored and starts as "only while live" on every app start.
- Plan (agreed 2026-11-03): EventSub `stream.online` / `stream.offline` on the
  existing WebSocket session (no scope needed); the starting state comes from
  Helix `GET /streams`, queried on every new session and again in the hourly
  token check, because events only report changes. The gate sits in
  `CommandProcessor` after the dry-run check, so the dry run is never gated.
  Work already running (boss countdown, open challenge) finishes normally.
  The Twitch card shows "live / offline" and the switch; changes go to the
  chat log.
- **Done 2026-11-03**: built and live-tested the same day in the test
  channel. See the log in work-packages.md.

## WP-08 — Operations (2–3 days) → v1.0.0

Mod commands, deletion flow, backup, republish-all, README, message wording
pass.
Demo: stream-ready.

- User decisions (2026-11-04, proposals accepted):
  - Mod commands bypass the WP-07 live gate (`CommandAccess.Moderators`, no
    offline hint for them): deleting a trainer after the stream must work.
  - Installation: a self-contained `win-x64` ZIP, no installer. A
    `release.yml` (like DigitalDeskCycle2's) builds it on the version tag,
    with the CHANGELOG section as release notes. Updating = replacing the
    program folder; data stays apart (`AppPaths`). Optional: an „Neue
    Version verfügbar“ hint from the GitHub releases API, no auto-update.
  - Settings: channel and website URLs move out of the `appsettings.json`
    next to the program (an update would overwrite it) into an
    „Einstellungen“ card, stored in the data directory. (Planned with a
    connection test, which was not built; noted in review 1.0.3 — the Twitch
    card shows after the restart whether the channel exists. Backlog.) No
    guided first-start wizard — the user sets the PC up once.
  - Cached fight files after a deletion: no Cloudflare purge (it would need a
    second token with zone rights). Fight files get a shorter max-age (about
    one day) instead of a year; a deleted name is gone from the cache within
    a day. Spec §9 and website-setup.md change with the implementation.
  - Chat wording pass: all bot texts in one table (command → text) for one
    review sitting.
- User decisions (2026-11-04): `!entfernen @name` asks for confirmation
  („!entfernen @name ja“ within 60 s by the same moderator); the deleted
  trainer's name and id are overwritten in the stored battle logs (owner →
  „[gelöscht]“, trainer id → null; ADR-0004 addendum). Backups keep the name
  until they rotate out.
- Plan (agreed 2026-11-04, ≈ 3–4 days): mod commands bypass the live gate,
  `!bossreset` clears the boss cooldown; a deletion service removes trainer,
  specimens, achievements, badges, NPC cooldowns and participant rows in one
  transaction, rewrites the logs and queues trainer page (deleted), leaderboard
  and affected fights — reachable via `!entfernen` and a confirmed button in
  the trainer list; fight files `max-age=86400`; `VACUUM INTO` backup on every
  app start before the migrations plus „Jetzt sichern“, last 10 kept, restore
  by hand (README); `settings.json` in the data directory as a configuration
  layer, an „Einstellungen“ card (channel, website addresses) applied after a
  restart; `release.yml` (self-contained `win-x64` ZIP with `data/`, notes
  from the CHANGELOG), an update hint from the GitHub releases API once per
  start, README section for the streamer; `docs/chat-texts.md` for the wording
  pass. Not included: manual corrections of trainers (backlog).
- User decision (2026-11-04): wording pass — all six proposals in
  `docs/chat-texts.md` accepted, the rest stays as drafted.
- **Done 2026-11-04**: built and live-tested the same day in the test
  channel. See the log in work-packages.md.
- Pitfall: SQLite backup with WAL, see `src/PokeArena.Core/CLAUDE.md`.

## WP-10 — „Neuigkeiten“ page on the website (≈ 0.5 day) → v1.3.0

A website page that tells players in simple German what changed in the game,
per version. Numbered WP-10 but runs before WP-09.
Demo: the menu links „Neuigkeiten“; the page lists the player-visible changes
of 1.1.0–1.3.0, newest first.

- User decision (2026-11-09): own work package, done before WP-09.
- User decision (2026-11-09, plan approved with „Ja, umsetzen“): a page of
  its own for players, not a copy of `CHANGELOG.md` — that one is written for
  the developer and the streamer (R2 keys, OBS port, „Alles neu
  veröffentlichen“, reconnects). The page carries only what players notice:
  commands, rules, durations, odds, website changes. One to three short lines
  per release.
- User decision (2026-11-09, same plan): static Razor page
  `Pages/News.razor` at `neuigkeiten`, menu link after „So geht's“, bUnit
  test that the newest version comes first. No R2, no Contracts change, no
  CHANGELOG parsing.
- User decision (2026-11-09, same plan): one heading per version („Version
  1.2.0 – 8. November 2026“); the entry is written on the package branch,
  the date set in the release commit. `/ship-wp` and `/release` carry the
  step.
- User decision (2026-11-09, same plan) — timing: `pages.yml` deploys the
  site on every push to `main` that touches `src/PokeArena.Web/**`, but the
  streamer updates the desktop app later, so the page can announce a change
  that does not apply on stream yet. Accepted: entries are labelled by
  version and the page points to `!version`. Publishing the entries from the
  app via R2 is parked in backlog.md in case it matters for WP-09.
  Superseded in part (user decision 2026-11-10, branch
  feat/pages-on-release-tag): `pages.yml` deploys on the release tag, so a
  merge into `main` no longer announces anything early; the gap until the
  streamer updates the app remains.
- Back-fill: player-visible changes of 1.1.0 (2-minute boss lobby, late-join
  extension, boss HP left, `!challenge @Name`, hints at most every 30 s) and
  1.2.0 (fight page shows how each Pokémon ended); nothing older, the page
  starts with the first update after the launch.
- **Done 2026-11-09** (release 1.3.0 after the live test). See the log in
  work-packages.md.

## Shorter boss cooldown for a while (`feat/boss-cooldown-reduced`) → v1.5.0

Streamer request (2026-11-10): halve the boss cooldown for a while from chat.
Not a setting, a temporary event — so a mod command like `!bossreset`.

- User decisions (2026-11-10): command `!bosscooldown <Dauer>`; a cooldown
  already running shrinks too („Ja“); the reduction runs by the clock, the
  stream ending does not stop it, at most 24 hours; the cooldown length is
  selectable („Eine wählbare Dauer wäre cool“); release as 1.5.0, WP-09 moves
  to 1.6.0.
- Built as the simplest variant, **Proposal (not confirmed)**: durations in
  whole minutes only (`!bosscooldown 120`, no „2h“); the chosen cooldown is an
  optional second number, half of `BossCooldownMinutes` without it, and must be
  shorter than `BossCooldownMinutes` (1–29) — mods cannot lengthen it; the
  running fight cooldown shrinks by the factor new / its own length; `!bosscooldown aus`
  leaves a running cooldown as it is; the natural end is not announced; the
  failed-lobby retry cooldown stays 2 minutes; stored in the database
  (survives a restart).

## „Schaden“ box on the fight page (`feat/damage-meter`) → v1.6.0

User request (2026-11-10): under the two fight cards a box as wide as both
with a damage meter — who dealt how much, bars from the left, ranked; maybe
the boss too, with the trainers summed.

- User decisions (2026-11-10): mockup accepted — „Gesamt“ (sides summed, the
  boss as its own bar) plus a ranking; bars in the Twitch chat colour, name
  and number inside the bar like the HP numbers („Ja, so bauen“).
- Built as the simplest variant, **Proposal (not confirmed)**: only damage
  that arrived counts; boss fight ranks the trainers with their share of the
  boss's HP, PvP/NPC rank every Pokémon sent in; equal damage shares a rank;
  the boss and NPC Pokémon in their first type's colour, no colour → grey,
  a side of several colours → the HP bar's green. The raw chat colour in the
  dark theme was dropped: user decision „die Balken-Farbe zum Standard“ (1.6.1),
  darkened in both themes.
- User decision (2026-11-10, release): released as 1.6.0 together with the
  crisp HP numbers („1.6.0, Test ok“); WP-09 moves to 1.7.0.

## WP-11 — Automatic update of the desktop app (≈ 1–2 days) → v1.5.0

The app updates itself from the GitHub releases instead of the user
uploading the ZIP to the server by hand. User decisions (2026-11-10): its own
package before version 2, independent of the website rebuild, and the next
package after `!bosscooldown` (1.5.0) — before WP-09, so the balancing release
already arrives by itself. Planned first as 1.6.0; at the merge the user
decided „Jetzt mergen, beides 1.5.0“: WP-11 ships with `!bosscooldown` in
1.5.0, WP-09 stays 1.6.0.
Demo: tagging a release brings the running app on the server to the new
version without anyone signing in to the server, never during a stream.

User decisions at the start (2026-11-10, plan of `wp/11-auto-update`):
„Ja, mit Velopack, manuellem Update-Check zusätzlich zum automatischen.
Deine Empfehlungen passen so.“ Settled with that (ADR-0010):

- Velopack (`Velopack` package, `vpk` as a local dotnet tool, same version);
  `release.yml` packs `Setup.exe`, `Portable.zip`, full and delta packages and
  `releases.win.json` onto the GitHub release, the app reads them through
  `GithubSource`. The old ZIP goes away.
- packId `PokeArena.App` (the data directory `%LOCALAPPDATA%\PokeArena` must
  never be the install folder); own `Main` with `VelopackApp.Build().Run()`
  first.
- First check 5 minutes after the start, then hourly; download at once;
  install automatically only while the bot takes no commands, no dry run and
  no boss lobby is open (checked every minute; open `!fight` challenges do not
  block); after the code review (user decision „Alle wie empfohlen“) only
  once the bot has been idle for 10 minutes. „Jetzt aktualisieren“ installs at once, also during a stream, but not
  while a boss lobby is open; „Nach Updates suchen“ checks at once.
- README recommends `Setup.exe`; `Portable.zip` for a folder of one's own.
- No switch to turn the updates off, the interval is fixed, no automatic
  rollback (backlog) — a failed start is logged.

Open:

- 1.5.0 is the one-time switch (`Setup.exe` as the same Windows user, then
  delete the old folder).
- A tag deploys the running bot within the check interval after the next
  stream — tag only after the live test (as today); a tag set by mistake goes
  live.
- The very first Velopack release (1.5.0) has no delta package; only from
  1.5.1 on does the real GitHub path (feed of an earlier release, delta) run —
  watch the first automatic update on the server.
- If version 2 makes the website a separate Windows service, Velopack does not
  update it (it is for apps of a signed-in user); keep the web host in the app
  process or plan its own update path there.

## „Statistik“ page on the website (`feat/statistics-page`) → v1.7.0

User idea (2026-11-10): a website page with rankings beyond the leaderboard.
Every list is derived from data the database already holds (`BattleParticipant`,
`Battle`, `Specimen`) — no migration and no reading of fight logs; deleted
trainers drop out by themselves (their participant rows and specimens go, their
id leaves the logs).

User decisions (2026-11-10, evaluation of the idea):

- Lists: most boss participations; most fights — **PvP only** (boss
  participations have their own list, NPC fights do not count);
  „Unglücksraben“ as the **longest streak of boss participations without a
  catch** instead of the plain count of missed catches, which would only
  repeat the participation list.
- Added after the evaluation: a community Pokédex as the page's head
  (distinct species the community owns of 151, plus totals of fights, catches
  and trainers), „Einzelstücke“ (species only one trainer owns, with the
  owner), „Erzrivalen“ (the pairs with the most PvP fights against each other,
  with their record) and a boss record (which boss appeared most often, which
  escaped most often).
- Lifetime damage is left out for now („erstmal weg“, backlog): it would
  read every fight log on every publish, and teams of 6 in PvP collect damage
  faster than one fielded Pokémon per boss fight.
- Before WP-09, released as 1.7.0; WP-09 moves to 1.8.0 („Vor wp-09“). Moved to 1.8.0
  (WP-09 to 1.9.0) when the profile picture went first (2026-11-11); merged
  first after all, so both ship in 1.7.0 and WP-09 stays 1.8.0 (user decision
  2026-11-11, „Ja, so lassen“).
- „Einzelstücke“ as a capped list, not every species with one owner („Eine
  begrenzte Liste“).
- Built with `/ship-wp` (2026-11-11); at the plan the lists as bars instead of
  tables („Ich würde Balken bevorzugen“) — the same bars as the „Schaden“ box
  (`Shared/BarList.razor`).

Built as the simplest variant, **Proposal (not confirmed)**:

- One new site file `statistik.json` (`SitePaths`, records in Contracts) and a
  page `/statistik` in the navigation; it polls every 15 s like the
  leaderboard. New file only, so the data format version stays the same.
- Top 10 per list; names link to the trainer page. Equal values share a rank.
  The streak counts the longest run ever and marks one that is still running.
- Queued with every fight (`SiteOutbox.ForBattle`) and with registration,
  rename and deletion (`ForTrainers`); „Alles neu veröffentlichen“ includes
  it. Mutable cache like the leaderboard.
- „Einzelstücke“: the number of them in the heading, then 10 — rarest tier
  first (legendary, rare, common), then by dex number. A plain list (species
  with type chips, owner link), no bars: there is no number to measure.
- PvP shows the wins next to the fights („9 Kämpfe · 6 Siege“); the boss
  record is two lists (appeared, escaped); rivals „Ben – Anna“ with „5 Kämpfe ·
  3 : 2“, the one with more wins first.
- Bar colours as in the „Schaden“ box: trainers in their darkened chat colour
  (no colour → grey), bosses in their first type's colour, rivals and the
  community bar in the HP bar's green (several colours).
- Equal values in a list are ordered by name; a tie at place 10 is cut at ten
  rows.
- A line on the „Neuigkeiten“ page, the file in the table of spec §9.

## Twitch profile picture on the trainer page (`feat/trainer-profile-image`) → v1.7.0

User idea (2026-11-11): the trainer page shows the viewer's Twitch profile
picture left of the name.

User decisions (2026-11-11):

- Planned right away instead of parking it in the backlog („Gleich planen“).
- The picture is mirrored to **our R2 bucket**, not loaded from Twitch's CDN:
  visitors' browsers talk only to our data host (no IP addresses to Twitch).
- Built right away, before the „Statistik“ page, as 1.7.0 („Mach das ruhig
  jetzt“ on the question before or after it); Statistik moves to 1.8.0,
  WP-09 to 1.9.0. The „Statistik“ page was merged first after all, so both
  ship in 1.7.0 and WP-09 stays 1.8.0 (user decision 2026-11-11, „Ja, so
  lassen“).

Constraints found while planning:

- EventSub chat messages carry the chat colour but no picture: it comes from
  Helix `GET /users?id=…` (`profile_image_url`, up to 100 ids per call, no
  scope; `HelixClient` already calls `users?login=`).
- SQLite is the single source of truth and every site file is regenerable
  (ADR-0001, ADR-0003) — a picture only on R2 would break that. So the
  **image bytes live in the database** and the outbox renders them like any
  JSON file.
- The WAF blocks every query string on the data host (website-setup.md §2),
  so cache busting with `?v=<hash>` is out; the rate limit counts only `.json`.
- `R2SitePublisher` sends a fixed JSON content type; images need their own.

Built as below (feat/trainer-profile-image); the details are a **Proposal (not
confirmed)**:

- **Data:** a new table `TrainerProfileImage` (trainer id as key, cascade
  delete with the trainer; `Content` blob, `ContentType`, `SourceUrl`,
  `CheckedAt`). Own table so the leaderboard's trainer queries never load
  blobs. A few hundred trainers × ~10–30 KB stay a few MB in the database and
  the backup.
- **Fetch:** a `ProfileImageRefresher` background service on the live
  database only (not the dry run), every 15 minutes while Twitch is connected:
  trainers without a picture or with one older than 24 h, 100 per Helix call.
  The picture is downloaded only when `profile_image_url` changed (Twitch puts
  a new id into the URL on every upload), so a daily round costs about four
  Helix calls. A trainer who got no picture (account gone, picture refused)
  also waits 24 h, remembered in memory (review: otherwise a Helix call and
  warnings every round); an unreachable picture (no connection, timeout,
  408, 429, 5xx) is tried again next round.
  New trainers get their picture within 15 minutes; no signal from
  registration needed. Logs name the trainer by id, never the picture URL
  (older URLs contain the login; privacy page).
- **Download guard:** https only, host `static-cdn.jtvnw.net` only, at most
  512 KB, content type `image/png` or `image/jpeg` and the matching file
  signature (the type comes from the bytes), 30 s per download including the
  body. The 150×150 variant
  (`-300x300.` → `-150x150.` in the URL), falling back to the original URL
  if that fails — no image library in Core.
- A user Twitch no longer returns (account deleted or banned): the picture
  row goes and the file is deleted; the trainer stays.
- **Publishing:** a change writes the row and queues
  `profilbild/<twitch-id>.png` or `.jpg` plus the trainer file in one
  transaction. `SiteFile` gets a content type (default JSON) that
  `R2SitePublisher` sends. The extension follows the format (changed while
  building: the local preview serves no extensionless files); a picture in the
  other format moves the path and the old one gets deleted.
  Cache a day like fights: a new picture or a deletion is visible on the next
  day at the latest (same reasoning as spec §11 for fights).
- „Alles neu veröffentlichen“ queues every picture and lists the
  `profilbild/` prefix, so leftovers without a source get deleted;
  `IsOwnPath` accepts only `profilbild/<numeric id>.png|.jpg`.
- **Contract:** `TrainerPage.ProfileImage` (`string?`, path relative to the
  data base URL, null without a picture). New optional field, so the data
  format version stays 1; older site builds ignore it.
- **Website:** a round picture (~56 px) left of the `<h1>` on the trainer page;
  without one nothing is shown (no placeholder). Leaderboard and fight pages
  stay as they are (backlog if wanted).
- **Privacy:** deleting a trainer (spec §11) deletes the row by cascade and
  queues the picture's path; the privacy note on the website mentions the
  mirrored picture. Spec §9 (file table), §11 and architecture.md (data model,
  refresher) get the change; ADR-0003 an addendum (binary files in the bucket,
  bytes in SQLite).
- Tests: refresher with fake Helix/HTTP (changed URL, unchanged URL, user
  gone, host or size refused), generator renders and deletes the picture,
  republish covers the prefix, bUnit for the page with and without picture.
- A line on the „Neuigkeiten“ page.

## Leaderboard pictures and chat colours (`feat/leaderboard-profile-colours`) → v1.7.1

User request (2026-11-11): the profile pictures small before the names on the
leaderboard, and trainers' names in their Twitch chat colour everywhere
(trainer page, leaderboard, …).

User decisions (2026-11-11):

- Scope „Ohne Kampfliste“: leaderboard (picture + colour) and trainer page
  (colour). The fight lists stay plain — their names sit inside bot-written
  sentences (backlog). Fight page and statistics had the colours already.

- Released as 1.7.1 like the earlier look-only changes (1.4.1, 1.6.1), so
  WP-09 stays 1.8.0 (user decision 2026-11-11 at the release, „1.7.1“).
  Superseded: 1.8.0 became the boss balancing ahead of WP-09, WP-09 is 1.9.0
  (proposal).

Open question for the streamer test: in the dark theme the darkened colours
reach only about 3.2 : 1 on the table (the open 1.6.1 test, „Balken-Farbe zum
Standard“); a whole leaderboard in chat colours shows it more than a few names
in the fight log. If it does not read well, the lighter dark-theme variant of
1.6.0 returns for names.

## Fight log: HP before the hit (`fix/battle-log-hp-before`) → v1.8.0

Viewer feedback (2026-11-18, on a duel): „Owei (…, HP 52) hat Digda (…, HP 0)
46 Schaden zugefügt“ shows the target's HP after the hit, so every knockout
reads „HP 0“; the viewer suggested the HP before the hit.

- User decision (2026-11-18): the small fix first, the fight log redesign later
  (ideas in backlog.md, „Fight log redesign“).
- Decided by release 1.8.0 and the 1.8.1 pick (mockup „B“, „80 → 22“ in the bar): the target shows both, „HP 10 → 0“, so a 46 on 10
  HP left explains itself; the attacker keeps one number (its HP does not
  change by its own hit). The text is rendered from the stored events, so older
  fights change after „Alles neu veröffentlichen“.

## Pokédex on the trainer page (`claude/pokedex-trainer-page-improve-a4a504`) → v1.10.0 (released 2026-11-23)

Viewer idea (2026-11-22, with a mockup): the trainer page's Pokédex as a list of
all 151 species (unknown ones as „????????“) next to a detail box with types
and base values; a switch to hide the unknown ones; species the trainer has
seen but not caught greyed out; a hidden search behind a click on „Name“.

User decisions (2026-11-23, evaluation):

- Base values come from one central file instead of being copied into every
  trainer file („Wäre zentral da nicht die einfachere Lösung?“): `arten.json`
  on R2, published by the bot from `data/pokemon.json` — derived and
  regenerable like every site file (ADR-0003: GitHub Pages holds the app only).
  The trainer file adds only the ids of the species seen.
- The search goes into „Neuigkeiten“ („Die Suche kannst du ruhig mit in die
  Neuigkeiten packen“).
- The plan below was approved as a whole („Ja, umsetzen“) and built the same
  day (log: work-packages/claude-pokedex-trainer-page-improve-a4a504.md); only
  the version stays a proposal.

Plan:

- Contracts: `SpeciesData(Id, Name, Types, Hp, Atk, Def, Spez)` and
  `SpeciesList(IReadOnlyList<SpeciesData> Species)` at `SitePaths.Species =
  "arten.json"`; `TrainerPage.Seen` as an optional list of species ids
  (omitted when null, so `SiteMeta.CurrentFormatVersion` stays, ADR-0009).
  Rarity (`seltenheit`) stays out.
- „Gesehen“ = every species in a fight the trainer took part in, both sides,
  including Pokémon „nicht eingesetzt“ — the fight page lists them too — minus
  the species the trainer owns now. A species lost in PvP therefore turns
  „gesehen“. Stored in a new table `SeenSpecies (TrainerId, SpeciesId)`,
  written with the battle in the same transaction; the migration fills it once
  from the stored logs (`json_each` over `$.fighters`). Not computed from the
  logs on every publish: a trainer file is rendered after each of their fights,
  and the statistics page deliberately avoided reading logs (feat/statistics-page).
  Deleting a trainer deletes their rows (cascade).
- `arten.json` is queued with „Alles neu veröffentlichen“ and the republish on
  the first start of a new version — its values change only with a release —
  and cached for a day like fights.
- Website: list and detail box side by side, equally high (list ≈ 10 rows,
  scrolls inside); the box's content at the top. Pre-selected is the first
  species caught. Unknown rows „????????“ in `--muted` (≥ 4.5 : 1, no
  `opacity`), not selectable. Seen rows: name in `--muted` plus a small
  „gesehen“ mark (not colour alone), type chips; their box shows name and
  types, the base values only for caught species — like a classic Pokédex.
  Column „Anzahl“ as „× 2“ from two on, „Starter“ chip as today.
- Switch over the list in the look of „Balken / Text“: „Alle“ (default) /
  „Nur gefangene“; not remembered at first — kept per viewer in `localStorage`
  (`pokearena.pokedex`) since 2026-11-23 (user request).
- Search: „Name“ in the list head is a button; a click turns it into a text
  field (focus at once), Esc or „×“ turns it back and clears it. It matches the
  name (case-insensitive) or the number („25“, „#025“) of caught and seen
  species only — `arten.json` holds every name, and the search must not reveal
  unknown ones. Combined with the switch.
- Phone (375 px): the two columns do not fit; the box opens below the selected
  row instead of beside the list.
- Without `arten.json` (between the release tag and the bot's update, review
  feat/statistics-page) or with an older trainer file: the list from the
  trainer file alone, the box without base values, no „gesehen“ rows — never
  an error.
- Stats row: an extra „Gesehen“ count — open question, left out unless wanted.
- WP-12 (English website) can later add English names to `arten.json`.
- Version: 1.10.0 together with the unreleased coloured HP (1.9.1), WP-09
  and WP-12 move up one version each — for the user to decide at the release.
- Tests: Core (seen rows written per fight kind, migration backfill on a
  sample log, `arten.json` rendered, queued on republish), Web bUnit (151 rows,
  switch, seen styling, search incl. no match on unknown names, missing
  `arten.json`, older trainer file), sample data (`beispieldaten/arten.json`
  generated from `data/pokemon.json` — base data, no player data).

## WP-09 — Balancing after the first streams (≈ 2 days) → v1.11.0 (proposal)

1.9.0 went to the fight log's text view and the „Einstellungen“ page (user pick
at the release, 2026-11-22), so WP-09 and WP-12 each move up one version. The
Pokédex on the trainer page (released as 1.10.0) moves them up once more.

Tuning with real stream data and the Monte Carlo simulator instead of
guesses. Starts after 1.0 has run on a few streams.
Demo: measured win rates per NPC and boss group size, adjusted values.

- User decision (2026-11-04): the balancing points move here from WP-03/05;
  numbers are only set after a simulator run.
- Done ahead of WP-09 (user decision 2026-11-04): `BossHpPerParticipant` 1.8 →
  1.5 for a higher catch chance (≈ 57–70 % per participant instead of
  50–64 %), ADR-0006 addendum. Next step if still too stingy: 1.3.
- Stream data (2026-11-19, ADR-0006 addendum): 182 boss fights won 50.5 %
  (target then ≈ 80 %), 37.7 % catches per participant, N = 4 only 34 %.
  Collections hold mostly the weak bosses a group could beat (fielded stat sum
  202 vs. bosses 265). Simulator part 4 (campaigns from starter + real
  catches) reproduces this (≈ 57 % / 44 %); compare balancing variants with
  `--part 4`.
- Done ahead of WP-09 (user decision 2026-11-19, „B2 mit 1.3“,
  `feat/boss-balancing`, released in 1.8.0): the boss attacks N / 3 targets per round on
  average (remainder carried over, rounded up first) instead of `⌈N/3⌉`, and
  `BossHpPerParticipant` 1.5 → 1.3. Part 4: 57 → 75 % group wins, 44 → 61 %
  catches per participant, every N between 70 and 76 % — the `N = 4` dip is
  gone. Not chosen (simulated, ADR-0006 addendum 2026-11-19): a boss HP bonus
  by rarity (1.3 + −0.15…+0.20), HP compensation by target share, the
  carry-over rounding down first, random rounding of the targets.
  Proposal (not confirmed): a boss HP that follows the boss's strength (stat
  sum) — strong bosses still win far more often (part 4: 96 % group wins
  below a stat sum of 250, 15 % from 400); look at it again after a few
  streams with the new rule.
- No runtime balancing editor (spec §13): `Scaling`, `k` etc. are jargon
  for the streamer and every change should go through the simulator. At most
  the streamer-facing values (boss cooldown, countdown) may get a setting.
- Done ahead of WP-09 (user decision 2026-11-09, `feat/boss-pick-per-species`,
  1.4.0): the boss pick is uniform over distinct species instead of
  specimens. Simulator part 3 (`dotnet run tools/BalancingSimulator.cs --
  --duels 1`, defaults otherwise; part 3 does not depend on `--duels`):
  group wins and catches move by at most 1–2 points at 5–100 catches. Per
  species, legendaries duplicate most (5 species), rares least (48), commons
  in between (98), so the pick fields legendaries less and rares more — at
  100 catches common / rare / legendary ≈ 73 / 21 / 5.9 % → 72 / 23 / 4.9 %
  (review of the branch). The common share (≈ 62 % at 5 catches → 73 % at
  100, the rare starter gets diluted) comes from the boss tier odds, not from
  duplicates. For a single trainer it is noticeable both ways: 3 × Digda +
  Arkani fields Arkani 50 % instead of 25 %, starter + 3 × Arktos + Digda
  fields Arktos 33 % instead of 60 %. If fielded Pokemon still feel too weak,
  levers are the tier odds or a pick weighted by tier or strength —
  simulator first.
- Progression: PvP loot prefers species the winner lacks, like the NPC
  reward (E22) — a per-trainer bonus on the boss does not fit, all survivors
  catch the same species. Then watch the data again.
- NPC difficulty: measure win rates per NPC with random teams at its unlock
  threshold; set `staerke` so the first NPCs win ≈ 50–60 % and later ones
  clearly less (Eich lost all 10 fights in the WP-05 dry run).
- The website's „So geht's“ page (`Pages/Guide.razor`, added 2026-11-08)
  repeats countdown, minimum team size, cooldowns and NPC thresholds as plain
  text — update it with every changed value. If countdown or boss cooldown
  become a streamer setting, the page has to word them loosely („kurz“, „etwa
  eine halbe Stunde“) or the values have to be published in `meta.json`.

## WP-12 — English website (≈ 2–3 days) → v1.12.0 (proposal)

Viewer feedback (2026-11-21): „Spracheinstellung zw. Deutsch und Englisch.
Automatisch gewählt durch Browserlanguage und dann halt selbst festlegbar.
Deutsch > Deutsch, Rest > Englisch.“ Branch `wp/12-english-website`.
Demo: a browser set to English opens the site in English, fight logs and
species included; „Einstellungen“ switches back to German at once.

User decisions (2026-11-21):

- Own work package, after WP-09 (WP-09 stays 1.9.0, this one 1.10.0).
  Addendum 2026-11-22: 1.9.0 was released with the fight log's text view and
  the settings page, so the numbers became 1.10.0 and 1.11.0 (proposal, not
  confirmed); the order stays. Addendum 2026-11-23: the Pokédex on the trainer
  page takes 1.10.0, so WP-09 and this one become 1.11.0 and 1.12.0 (proposal).
- Website only. Chat texts and the desktop app stay German.
- The English site shows English species, type and NPC names (Bulbasaur,
  Fire, Brock).
- „Datenschutz“ is translated; „Neuigkeiten“ stays German, with a short
  English note on top for English viewers (no second text per release).

Plan — Proposal (not confirmed):

- Language pick: a third section „Sprache“ on „Einstellungen“ with
  „Automatisch / Deutsch / English“, kept in `localStorage`
  (`pokearena.sprache`) like the look. „Automatisch“: `navigator.language`
  starting with `de` → German (de-AT, de-CH too), anything else → English.
  The script at the top of `index.html` sets `<html lang>` before the first
  paint; Blazor reads the language once at start and switches live through a
  cascading value. Routes stay German (`/kampf/12`), so chat links keep working.
- Site texts: one typed text class per language (an interface or record, both
  checked by the compiler), no `.resx` — the web project runs with
  `InvariantGlobalization`, where `de`/`en` cultures cannot be created, and
  turning it off ships ICU data. English dates as „21 Nov 2026, 18:30“, still
  German time.
- Text rendered by the bot (`Headline`, `Result`, `Lines`, `Log` parts in
  `BattleTexts`/`SiteGenerator`): the bot writes each file with text once per
  language — German at today's paths, English under `en/` (`en/kampf/12.json`,
  `en/kaempfe.json`, `en/trainer/…`, `en/statistik.json`); `meta.json`,
  `rangliste.json` and profile pictures stay shared. The contracts stay as they
  are (no `FormatVersion` bump); the site falls back to the German file on a
  404, so it works before the bot's update. Fight logs are stored as events,
  so the republish after the update renders old fights in English too. Cost:
  about twice the R2 uploads and twice as long „Alles neu veröffentlichen“ —
  fine at a few hundred trainers. Needs an ADR (addendum to ADR-0003/0009).
  Not chosen: both languages in one file (a big boss fight has hundreds of
  parts, contract break); the site wording the log itself from events (moves
  the battle text from Core to Web, large rebuild).
- Names: `tools/GeneratePokemonData.cs` also takes the English name from
  PokeAPI (`name_en` in `pokemon.json`); English type names as a fixed table;
  `npcs.json` gets an English name per NPC (Eich → Oak, Rocko → Brock, Bob →
  Lt. Surge, Pyro → Blaine, Agathe → Agatha, Siegfried → Lance, Garry → Gary).
  Type names in the data stay the German keys — `Format.TypeClass` builds the
  CSS classes from them — and the site shows the label of the language.
- Chat commands keep the German names (`!registrieren bisasam`,
  `!challenge Eich`): the English „How to play“ page names the German word for
  every command argument. English names as chat aliases are a backlog idea,
  not part of this package.
- Tests: bUnit tests keep German as default; new tests for the English texts,
  the detection rule, the switch and the 404 fallback; a test that both text
  classes cover every key (the compiler does that if it is an interface).

Open before starting:

- English page title and brand line („PokeArena“ stays); whether the
  menu/footer shows a small DE/EN switch besides the gear page.
- Who checks the English wording (simple English for casual viewers).

## Version 2 — website served from the bot's server (parked) → v2.0.0

User decision (2026-11-10): remember this for version 2; nothing of it is
built before. Starting point: the bot runs on the user's Windows server —
apparently for good — instead of on the streamer's PC, and the OBS overlay is
not used. The user's Cloudflare zone already serves HorizonNET
(`C:\Dev-Repos\HorizonNET`) from such a server through a Cloudflare Tunnel, so
a tunnel is set up in principle.

Proposal (not confirmed):

- The website becomes an ASP.NET Core host on the server behind the Cloudflare
  Tunnel and reads the game data directly instead of JSON files on R2; live
  updates instead of polling become possible. Pattern from HorizonNET: Windows
  service, `ForwardedHeaders` trusted only from loopback (`cloudflared`), rate
  limiter.
- The overlay becomes a page of the website (`/overlay`), so OBS can load it on
  any PC; it replaces the loopback `OverlayServer` of the desktop app.
- R2 bucket, publish outbox, `SitePublishWorker`, „Alles neu veröffentlichen“
  and GitHub Pages could then go away.

Open before starting:

- Separate process (the site survives a bot restart or update) or part of the
  desktop app; how a separate process reads the SQLite file safely (WAL,
  read-only) or whether the bot hands it the data.
- The machine that holds the Twitch and R2 secrets becomes reachable through
  the tunnel; Cloudflare cache and rate limiting in front of it.
- A new ADR replaces ADR-0003 and changes ADR-0009 (today Web references
  neither Core nor EF Core).
- Who operates the desktop app now — the streamer by remote desktop or the
  user? That decides the audience of UI texts and setup steps (§2 of
  `CLAUDE.md`).
- Docs that still assume the streamer's PC: spec §9 (OBS overlay on the same
  machine), `docs/website-setup.md` §4 (OBS browser source), ADR-0001/0002
  context (README and `release.yml` were brought in line in WP-11).
- Smaller alternative without the rebuild: keep R2 + Pages and only add
  `/overlay` to the website, polling R2 — lags by the poll interval plus cache,
  enough for „latest fight / top 5“, too slow for the live boss countdown in
  backlog.md.

## Open UX questions

None. Decided earlier: the German chat wording (2026-11-04, docs/chat-texts.md)
and no guided first start (2026-11-04, WP-08).
