# Roadmap

Planned work packages up to v1.0.0, plus the packages after it (WP-10 news
page, then WP-09 balancing). Read the package before starting it and
agree the plan with the user. Estimates: solo, "day" = one focused person-day;
total ≈ 25–31 days. Every package ends in a state that could run on stream.

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
  base HP × 1.8 × N; target group win rate ≈ 75 %.
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
- Open: the `N = 4` dip (≈ 64 % group wins vs. ≈ 75 % target) — watch on
  stream whether players notice it. → WP-09 (user decision 2026-11-04).
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
- Back-fill: player-visible changes of 1.1.0 (2-minute boss lobby, late-join
  extension, boss HP left, `!challenge @Name`, hints at most every 30 s) and
  1.2.0 (fight page shows how each Pokémon ended); nothing older, the page
  starts with the first update after the launch.
- **Done 2026-11-09** (release 1.3.0 after the live test). See the log in
  work-packages.md.

## WP-09 — Balancing after the first streams (≈ 2 days) → v1.4.0

Tuning with real stream data and the Monte Carlo simulator instead of
guesses. Starts after 1.0 has run on a few streams.
Demo: measured win rates per NPC and boss group size, adjusted values.

- User decision (2026-11-04): the balancing points move here from WP-03/05;
  numbers are only set after a simulator run.
- Done ahead of WP-09 (user decision 2026-11-04): `BossHpPerParticipant` 1.8 →
  1.5 for a higher catch chance (≈ 57–70 % per participant instead of
  50–64 %), ADR-0006 addendum. Next step if still too stingy: 1.3.
- `N = 4` dip: likely caused by the jump of `⌈N/3⌉` targets from 1 to 2 at
  N = 4 (with HP 1.5: 57 % catch chance vs. 70 / 64 % at N = 3 / 5). Watch on
  stream first; if noticeable, adjust the target formula or the HP per player
  for that step in the simulator.
- No runtime balancing editor (spec §13): `Scaling`, `k` etc. are jargon
  for the streamer and every change should go through the simulator. At most
  the streamer-facing values (boss cooldown, countdown) may get a setting.
- Done ahead of WP-09 (user decision 2026-11-09, `feat/boss-pick-per-species`,
  1.3.1): the boss pick is uniform over distinct species instead of
  specimens. Simulator part 3: group wins, catches and the share of commons
  fielded move by at most 1–2 points at 5–100 catches — duplicates are
  spread over all tiers (98 / 48 / 5 species), so the common share (≈ 62 %
  at 5 catches → 73 % at 100, the rare starter gets diluted) comes from the
  boss tier odds, not from duplicates. It only helps the unlucky single
  trainer (3 × Digda + Arkani: Arkani 25 % → 50 %). If fielded Pokemon still
  feel too weak, levers are the tier odds or a pick weighted by tier or
  strength — simulator first.
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

## Open UX questions

- Done: exact German wording of all chat messages — reviewed 2026-11-04
  (docs/chat-texts.md).
- First start for the streamer: decided 2026-11-04 — no guided setup, the
  user configures the PC once (see WP-08).
