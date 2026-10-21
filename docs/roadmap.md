# Roadmap

Planned work packages up to v1.0.0. Read the package before starting it and
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
  test ran with the bot as moderator; without it untested).- **Done 2026-10-13**: live test passed — bot account signed in via device
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
  first reproduce the ADR-0006 numbers, then fine-tune `Skalierung`, `k` and
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
- User decision (2026-10-19, ADR-0006 addendum): `Skalierung` 20 instead of
  25 (13 % one-hit KOs, first strike wins 57.5 %); `k` stays 50.
- Open: the `N = 4` dip (≈ 64 % group wins vs. ≈ 75 % target) — watch on
  stream whether players notice it.
- Open: when does the runtime balancing editor come (spec §13 says "editable
  in the UI")? Proposal: WP-07 together with the other operations screens.
- Open: progression length. Expected catches for k distinct species (uniform
  lower bound) — 15: ~16, 60: ~76, 100: ~163, 151: ~845; with ≈ 60 % catch
  chance per boss that is ≈ 1.7× as many boss fights. Intended, or add a
  bonus for missing species (backlog)?

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
- Built 2026-10-21; live test (dry run) pending. See the log in
  work-packages.md.

## WP-06 — Website (4–5 days) → v0.6.0

Blazor pages (leaderboard, battle, trainer, overlay), GitHub Actions deploy of
the app, R2 bucket + CORS + cache headers, R2 publisher + outbox, local
overlay endpoint.
Demo: a battle link in chat opens a live page.

- Open: how Web shares the JSON DTOs without referencing Core (shared
  contracts project vs. duplicated records).
- Open: whose Cloudflare account hosts the bucket (user or streamer)?
- Constraint (ADR-0008): no official sprites, artwork or sounds — species are
  shown as text and CSS; decide whether the site shows the fan disclaimer.
- Open: define "earliest attainment" for leaderboard ties when a trainer
  loses and regains a species.

## WP-07 — Operations (2–3 days) → v1.0.0

Mod commands, deletion flow, backup, republish-all, README, message wording
pass.
Demo: stream-ready.

- Open: installation and updates on the streamer's PC (self-contained
  publish, installer?).
- Pitfall: SQLite backup with WAL, see `src/PokeArena.Core/CLAUDE.md`. A
  release workflow like DigitalDeskCycle2's (`release.yml`, notes from the
  CHANGELOG) fits here once installation is decided.

## Open UX questions

- Exact German wording of all chat messages (spec §14) — drafted during
  implementation, reviewed in WP-07.
- First start for the streamer: is a guided setup (Twitch login, R2 token)
  needed, or does the user configure the PC once?
