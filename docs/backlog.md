# Backlog

Unplanned ideas. Never implemented on the side — move into a work package in
roadmap.md first. Format: idea — date, origin.

## Continuation of WP-01 (chat)

- `!hilfe` command listing the commands — 2026-10-11, design review.
- `!dex` command: own distinct/total count in chat for viewers who don't open
  the website — 2026-10-11, design review.

## Continuation of WP-03 (boss, progression)

- Catch bonus for species the trainer does not own yet, to shorten the grind —
  2026-10-11, design review. (The PvP-loot variant is planned in WP-09; a boss
  bonus does not fit, all survivors catch the same species.)
- Set the boss countdown length without a release (streamer request) — not as
  a chat command (spec §8: settings live in the desktop app), but as a desktop
  setting, which WP-09 allows for countdown and boss cooldown; the „So geht's“
  page then words the time loosely or reads it from `meta.json` —
  2026-11-08, streamer feature request.

## Continuation of WP-04 (PvP)

- `!fight nein` to decline a challenge; today anyone can block a trainer for
  the timeout by challenging them — 2026-10-11, design review.
- Draw teams without duplicate species, so hoarding weak duplicates does not
  dilute a team — 2026-10-11, design review.
- Challenge mode from the PDF: challenger shows their team, opponent picks a
  team, winner picks the reward — 2026-09-03, spec §14.
- Cooldown between the same trainer pair if trading-by-losing is abused —
  2026-09-03, spec §6.

## Continuation of WP-05 (NPC, tokens)

- Redeeming tokens with the streamer: chat command plus a desktop screen to
  confirm and book redemptions — 2026-10-21, user decision E22.
- Token ledger (who earned or spent how many, when, why) instead of the plain
  balance, once tokens can be spent or corrected — 2026-10-21, WP-05 plan.
- Chat command for the own token balance and badges (or fold it into `!dex`)
  — 2026-10-21, WP-05 plan.

## Continuation of WP-06 (overlay)

- Live boss countdown in the OBS overlay (seconds left, participants) — the
  on-screen call to join; only after the streamer's feedback on the overlay —
  2026-11-04, open-points review.
- Overlay as a local HTML file that polls the app (CORS), so OBS shows it even
  when OBS starts before PokeArena; for now the overlay card explains the start
  order — 2026-11-08, review 1.0.3 (A8).
- Website reads the previous data format (N-1) after a format change, instead
  of only the hint „Die Daten werden gerade umgestellt“ — 2026-11-08,
  review 1.0.3 (A33).
- Fight page: cancel the retry loop when the fight id in the address changes, and
  reset the error flag — 2026-11-08, R1 second review (pre-existing).

## Battle system

- Attacks per Pokemon with per-attack types — 2026-09-03, spec §14.
- Pokemon defeated in a fight become unavailable for further fights —
  2026-09-03, spec §14.
- Battle log retention/cleanup policy — 2026-09-03, spec §14.

## Operations

- Automatic rollback when a new version fails to start (today: start message
  box, error in the log, backup from before the migration) — 2026-11-10,
  WP-11 plan.
- Switch to turn the automatic updates off, or to install only by hand —
  2026-11-10, WP-11 plan (not needed while the developer runs the server).
- Sign `Setup.exe`, so SmartScreen stops asking — 2026-11-10, WP-11 plan.
- „Jetzt aktualisieren“ re-renders only on update events, so its
  disabled-while-lobby state can lag until the next one (the click itself
  checks the lobby again) — 2026-11-10, WP-11.
- Manual corrections of a trainer in the desktop app (give or take a
  specimen, tokens) — 2026-11-04, WP-08 plan (architecture sketch).
- Backups to a second location (e.g. a cloud folder the streamer picks) —
  2026-11-04, WP-08 plan.
- Connection test in the „Einstellungen“ card (channel exists, R2 bucket
  reachable) — planned in WP-08, not built; 2026-11-08, review 1.0.3 (D5).
- „Neuigkeiten“ entries published by the desktop app as JSON to R2 instead
  of static text in the website, so the page never announces a change before
  the streamer has updated the app — 2026-11-09, WP-10 (accepted the static
  variant; revisit if WP-09 values show up too early).

## Abuse protection

- Alt-account protection, e.g. minimum Twitch account age via Helix `Get
  Users` → `created_at` at `!registrieren` — 2026-10-11, user deferred it
  (small channel).

## Development process

From the comparison with FerretSharp (2026-11-09); not needed at the current size.

- One log file per package in `docs/work-packages/` with an index that has an
  "Areas" column (code → package) — once `docs/work-packages.md` grows past
  about 50 KB.
- Pitfalls as `.claude/rules/*.md` with `paths` headers instead of
  `src/<Project>/CLAUDE.md` — once `src/PokeArena.Core/CLAUDE.md` grows well
  past 10 KB.
- Local coverage script (`tools/coverage/`, ReportGenerator as a local tool)
  for the TE agent of `/complete-code-review`; no badge.
