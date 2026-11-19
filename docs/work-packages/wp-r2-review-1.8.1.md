# R2 — Review 1.8.1 (2026-11-19, branch wp/r2-review-1.8.1)

- Built: the fixes of the whole review 1.8.1 ([review-1.8.1.md](../review-1.8.1.md), six agents on `9ff8851`).
  Tests 484 → 548 (Core 374 → 430, UI 32 → 34, Web 78 → 84), Integration 9 → 10 (one skips without R2).
- User decisions (2026-11-19): „Alles mit Vorschlägen“ — one package R2, version 1.8.2 (proposal, not confirmed);
  every question took its proposed default (recorded in the review document).
- Part A:
  - A1 fixed: every overlay start error except cancellation disables only the overlay; card text without „anderes
    Programm“ (`OverlayStatus.Failed`); integration test with port 70000.
  - A2 docs: WAF allowlist expression, rate limit also on `/profilbild/`, billing alert required, check steps in §5,
    ADR-0003 addendum. The Cloudflare dashboard change is the user's — not verifiable here.
  - A3 fixed: colour branch and profile picture save in a transaction (only around enqueue + save, never around the
    downloads). No test: the race needs two contexts at the same millisecond; the same shape as the rename branch,
    whose transaction C2 pins.
  - A4 fixed: `TwitchConnection.IsTransient` (network, 429/5xx, timeout as cancellation) in the device login, the
    hourly check and the start rechecks; test with a 503 and a timeout while polling the login (red without the fix).
  - A5 fixed: a successful check that finds nothing drops a downloaded update.
  - A6 fixed: a start logger writes to the log file before the configuration is read, closed before the final one.
  - A7 built: `RepublishAfterUpdate` republishes once per new version (marker `website-version.txt`), waits for an
    R2 key; hint in the card.
  - A8, A9 fixed: republish errors show a `.problem` line; key removal asks first.
  - A10 fixed: privacy page „auf dem Rechner, auf dem der Bot läuft“.
  - A11, A13 fixed: thresholds pinned to spec §1 (`NpcCatalog.Thresholds`); missing lists and null entries in all
    three base files give readable problems.
  - A12 fixed: any whitespace ends the command name.
  - A14 fixed: `BattleTexts.BossEscaped` shared by chat and website/desktop; sample fight 3 updated.
  - A15, A16 fixed: CI actions pinned without stored token; ten real-time overloads banned (each checked to break the
    build).
- Part B: B1 `Shared/HpBar.razor` and `FighterAt`; B2 `OverlayServer.IsRunning`, `SiteLinks.None` dropped (test fake
  `NoSite`); B3 `FindByNameAsync` dropped, checklist and Core pitfall name `ResolveNameAsync`.
- Part C (each new test checked red under its mutation, the timer tests in six repeated runs):
  - C1, C8 deletion: only „ja“ confirms; statistics and meta queued.
  - C2 one transaction for PvP, boss, NPC, rename, deletion (`GameFixture.FailOutboxInserts`).
  - C3 `DefaultsTests` with literal values of spec §13 and the chat/site limits.
  - C4 refresh not cancelled by its caller; `FakeHttpHandler` gates now end when the caller cancels.
  - C5 limiter test on its own `TimerCountingTimeProvider`; C6 hit on fighter 1; C7 exactly 14 species unlock nothing.
  - C9 stop wait; C10 late join at exactly 10 s, other-mode join, shortening after a restart.
  - C11 done: colour kept on rename, re-ask stored, per-NPC tokens, catchers only on a defeated boss, gap boundary,
    re-queue resets attempts, failed listing, `OpenLive` in worker and refresher, chunked body, port, numeric ids,
    vip, withdrawn release (A5). Not done: „rechecks stop once live“ — a negative test needs a quiescence hook in
    `TwitchConnection` (backlog).
  - C12 done: negative positions and HP, the 25 % boundary on card and hit. Not done: `@key` on leaderboard rows and
    unsubscribing in `Dispose` — bUnit cannot observe element identity or events after disposal reliably (backlog).
  - C13 done: armed-timer waits instead of fixed 20 ms waits, bounded reads. `PumpUntilAsync` (1.6 s) left: it pumps
    towards a positive condition and cannot give a false green.
  - C14, C15 done: UI and Web tests one class per area with `DesktopTestContext` / `WebTestContext`, fakes in `Fakes/`,
    `GitHubReleaseHintTests` and `ChatMessageEventTests` on their own, one `FakeUpdater`/`FakeGate` linked into UI.Tests.
- Part D: D1 CLAUDE.md 18.0 → 12.0 KB (version table and release history gone, skills say „replace“); D2 README
  status and roadmap; D3 this split (index with „Areas“, backlog item done); D4 decided proposals marked, `/release`
  asks about open ones; D5 protocol rule; D6 pitfalls trimmed (all five 39.8 → 33.2 KB); D7 „Windows machine“; D8
  roadmap and architecture; D9 „deploys with the release tag“.
- Addenda: version 1.8.2 is a proposal (not confirmed). Proposal (not confirmed): the republish marker as a file in the
  data directory instead of a database table (no migration for one value; a restored backup does not republish by
  itself — the README step stays).
- Pitfalls: PowerShell treats „ and “ as quotes — edit German text with the editor, not with a script.
- Deliberately not done (→ backlog): the C11/C12 tests above.
