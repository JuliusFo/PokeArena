# R1 — Review 1.0.3 (2026-11-08, branch wp/r1-review-1.0.3)

- Built: the fixes of the first whole-solution review
  ([review-1.0.3.md](../review-1.0.3.md), six agents on `a2fdf4f`), and the
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
