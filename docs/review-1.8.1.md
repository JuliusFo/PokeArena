# Whole-solution review 1.8.1 (2026-11-19)

Request (2026-11-19): `/complete-code-review`, run right after the 1.8.1 release, which had suggested it (28 packages and branches since review 1.0.3). Second review of the whole solution, with the six agents of `.claude/skills/complete-code-review/`. All agents only read, on a fixed copy of `9ff8851` (= `main` = `v1.8.1`):

- GL: game logic and data
- TW: Twitch, chat queue, concurrency, host, build/CI
- WE: publishing, website, overlay, desktop UI
- SE: security and privacy
- TE: tests; the only agent that builds and runs tests, plus about 200 mutations on copies
- RD: reuse, dead code, structure, docs

**Status:** implemented with R2 (`docs/work-packages/wp-r2-review-1.8.1.md`, branch `wp/r2-review-1.8.1`), release 1.8.2 (proposal) after the live test; not done: three tests of C11/C12 (backlog); A2 needs the Cloudflare change by hand.

Duplicate findings are merged (all IDs named). **Confirmed** means: additionally checked by hand in the code; otherwise the agent's assessment applies.

Severity (as in `.claude/skills/branch-code-review/checklist.md`): **H** wrong or lost game data, broken invariant, security or privacy hole, bot hangs, answers twice or not at all · **M** wrong behaviour in realistic cases or a clear maintenance burden · **L** minor.

Effort (parts B and D): S/M/L = small/medium/large.

Build and tests on the snapshot:
- `dotnet build -warnaserror` without warnings.
- 484 tests green: Core 374, UI 32, Web 78. Also under invariant, en-US, tr-TR, ar-SA and ja-JP culture, and in 15 aggressive parallel runs.
- Integration tests only read: Twitch and R2 skip cleanly, the rest is local.
- No vulnerable packages. Patch and minor updates exist (EF Core/AspNetCore/Extensions 10.0.15, WebView.Wpf, TimeProvider.Testing, bunit, xunit.v3, AWSSDK.S3, CredentialManager); the 11.0 majors are for .NET 11.
- Mutations: about 200, about 50 stayed green, 12 of them equivalent (not gaps). No production bug found by TE.

## Checked without findings (excerpt)

- **Invariants:**
  - Trainers keyed only by Twitch id, including the profile-image rows (FK with cascade) and every R2 path.
  - Species stored by id only; starter never loot (filter plus filtered unique index; no other transfer path).
  - Achievements only added; NPC access uses the unlock rows (but see A11).
  - One `BattleRandom(seed)` per encounter in a fixed order; seed and full log stored.
- **Game rules:**
  - All 18 rows of spec §13 match the code, as do `npcs.json` (17 NPCs, one token each) and the guide page.
  - Boss rule of 1.8.0: HP 1.3 × N, targets with the carry-over `⌈rN/d⌉ − ⌈(r−1)N/d⌉`, as in spec §4 and the ADR-0006 addendum.
  - `!bosscooldown`: ranges, default half, only fight cooldowns shrink, stored across restarts.
  - Chat texts rows 1–43 match the code word for word in both directions, including the hint throttle split.
- **Transactions:** boss, PvP, NPC, registration, rename, deletion and republish write game data and `SiteOutbox` in one `BEGIN IMMEDIATE` transaction (except A3).
- **Review 1.0.3 fixes in effect:** A1–A7, A10–A30, A32–A34 (A31 not reproduced then); B1–B4, B6–B9; C1–C9, C11, C13, C15–C17, C19; D1–D4, D6–D8. Only partly: C10 (A test that only catches the bug by timing, C5 here), C18 (C14 here), D10 (D1 here).
- **Twitch:** refresh serialized and stored at once, reconnect order, keepalive via `TimeProvider`, 15 s connect timeout, wrong account detected, only 403/channel/auth/revocation final, event precedence over the poll.
- **Self-update (WP-11):** auto-apply off, source limited to the repository, `UpdateGate` (10 min idle, dry run, lobby), one install, release job read-only with SHA pins and version/CHANGELOG checks.
- **Security:**
  - Profile pictures: numeric ids only, https on `static-cdn.jtvnw.net` (also after redirects), PNG/JPEG by signature, 512 KB cap, timeout via `TimeProvider`, bearer token never sent to the CDN, R2 key from the stored id, `<img>` only for own path shapes.
  - Chat colours only as `#RRGGBB` and written into styles only as recomputed hex values.
  - HP bar per hit: only clamped numbers in the style, indexes and HP range-checked.
  - Scroll script: only numbers in `history.state`, no DOM sink.
  - Echo only for the login pattern, hints throttled, `lead_moderator`, no names or chat text in the log, privacy page complete.
  - No tokens, keys or player data in the git history (325 commits).
- **Publishing:** fresh rows first after a republish, revision guard, bounded backoff, republish lists `trainer/`, `kampf/`, `profilbild/` and raises the fight sequence; every new site field is optional, so the hours between the site deploy and the bot's update degrade cleanly.
- **Structure and docs:** layer rules §5 kept; every package used; ADR 0001–0010 without gaps; all relative links resolve; CHANGELOG sections = tags (25); every CSS class and JS function used; backlog without done items.

## Part A – Bugs and security

### High

None.

### Medium

| # | IDs | Location | Finding | Certainty |
|---|---|---|---|---|
| A1 | WE1 | `src/PokeArena.Desktop/Services/OverlayServer.cs:63-74` | **A bind error other than "port in use" stops the whole bot from starting.** Only `IOException` is caught; Kestrel turns only `AddressAlreadyInUse` into one.<br>Scenario: on a Windows server with Hyper-V, WSL or Docker, Windows reserves port ranges, which can change after a reboot. If 5299 falls into one, `Bind` throws `SocketException` 10013 (access denied). `StartAsync` of the host fails, `App.Start` shows „PokeArena konnte nicht starten“ and quits – after an unattended update restart the bot stays offline, although the overlay is not even used. A hand-edited port above 65535 does the same. The Desktop CLAUDE.md promises "a busy port only disables the overlay, never the bot".<br>Proposal: catch every exception except cancellation around `app.StartAsync`, report it through `OverlayStatus` („konnte nicht starten“), test with a stubbed bind failure. → Question 2. | path confirmed, trigger (reserved port on the server) suspected |
| A2 | SE1 | `docs/website-setup.md:36-68`, ADR-0003 addendum 2026-11-11 | **Requests for files that do not exist reach R2 without any limit.** The WAF rule blocks only query strings and methods, the rate limit counts only paths containing `.json`, and 404 is not cached.<br>Scenario: a script requests `profilbild/<random digits>.png` (or `/RANGLISTE.JSON`; `contains` is case-sensitive) at 50 per second: every request is a cache miss and a billed R2 read – about 130 M per month, about $40, more with parallel clients. That is the cost attack the ADR-0003 addendum and A9 of review 1.0.3 meant to close; 1.7.0 declared the picture prefix safe, but only existing (cached) pictures are.<br>Proposal (docs and the live Cloudflare configuration, no code): a WAF rule that blocks every path on the data host that is not `*.json` or `/profilbild/*.png|.jpg`; count `/profilbild/` in the rate limit if the free rule allows it, otherwise record the rest as an accepted risk and make the R2 billing alert mandatory; check steps in §5 (`/X.JSON` and `/foo` → 403). → Question 3. | docs confirmed; billing of 404 suspected (Cloudflare bills failed requests except auth errors); live configuration not checkable |

### Low

| # | IDs | Finding |
|---|---|---|
| A3 | GL1, WE2 | The colour-only branch of `TrainerService.ObserveAsync` (`:178-186`) queues site paths without a transaction – the race A25 fixed for registration and rename: if the worker deletes the outbox row between read and save, `DbUpdateConcurrencyException` drops the viewer's command without a reply. `ProfileImageRefresher.RefreshAsync` (`:168-178`) has the same pattern; it catches the error but throws away up to 100 downloaded pictures. Wrap both in `BeginTransactionAsync`. |
| A4 | TW1 | An HTTP timeout (30 s, `TaskCanceledException`) is not treated as transient in `TwitchConnection` (`:359`, `:384`) and the device login (`:192`, also 5xx/429 there): a slow `/validate` in the hourly check ends a healthy EventSub session (chat messages in the gap are lost), a single 503 while polling the login gives `LoginFailed`. One `IsTransient(Exception)` helper for all three loops, plus a test. |
| A5 | TW2, TE12 | `UpdateCheck.cs:122-127`: a downloaded update stays `Ready` when a later successful check finds nothing, so deleting a release tagged by mistake does not stop its install after the stream. Drop `Ready` → `Current` on a successful empty check (keep it on a failed check), or record it in ADR-0010. → Question 4. |
| A6 | TW3 | `App.xaml.cs:66-91`: until the logger is configured (`:82`), errors are not logged – a malformed `settings.json` or a bad config value only shows the message box, and on the unattended server the log holds nothing. ADR-0010 promises logging. Start a bootstrap file logger first. |
| A7 | WE7 | Since 1.4.1 nearly every release needs „Alles neu veröffentlichen“ once after the update, but WP-11 installs unattended at night and the card's hint only names the first setup. Republish once automatically on the first start after a version change (A7 of review 1.0.3 keeps new fights first), or at least extend the hint. → Question 5. |
| A8 | WE4 | `WebsitePanel.RepublishAsync` (`:96-107`) has no `catch`: an exception the publisher does not convert (SDK client error, timeout, SQLite busy) reaches Blazor's error UI and the „unerwarteter Fehler“ box. Catch everything but cancellation and show a `.problem` line. (suspected – which SDK exceptions escape is not checked) |
| A9 | WE3 | „Schlüssel entfernen“ (`WebsitePanel.razor:67`) deletes both R2 secrets with one click, without confirmation; the streamer never had the key. Inline confirmation like the trainer deletion, or drop the button. → Question 6. |
| A10 | WE5 | The privacy page (`Web/Pages/Privacy.razor:19, 24-25`) says data and backups are „auf dem PC des Streamers“; the bot runs on the developer's server (CLAUDE.md §8). Neutral wording („auf dem Rechner, auf dem der Bot läuft“). |
| A11 | GL2 | Achievements are stored by threshold number only and `NpcCatalog` does not pin the thresholds to spec §1: changing a `schwelle` in `npcs.json` silently revokes the unlock and makes `ForThreshold` throw when that trainer's page is published. Validate the thresholds against the §1 list in `NpcCatalog.Parse`. Latent (WP-09 plans `staerke`, not thresholds). |
| A12 | GL4 | `ParsedCommand.TryParse` (`ChatCommand.cs:49`) separates the command only at space and tab, while the arguments split at any whitespace since A18: `!fight<U+3000>@ash` is an unknown command, the bot stays silent. Use the first `char.IsWhiteSpace`. |
| A13 | GL5 | A17 only partly: a missing top-level key in `pokemon.json`, `npcs.json` or `typen.json`, or `"team": [null]`, gives a `NullReferenceException` instead of the readable `InvalidDataException`. `?? []` on the collections, skip null slugs with a problem entry. |
| A14 | GL6, RD9 | The boss that escapes because all Pokémon fainted reads „… ist entkommen – alle Pokémon wurden besiegt. …“ in chat (`BossEncounter.cs:254-257`) but „… ist entkommen. …“ on the fight page and in the desktop list (`BattleTexts.cs:58-60`, `:343`). One helper `BattleTexts.BossEscaped(…)` from the log, or record the difference as intended. → Question 7. |
| A15 | TW4 | `ci.yml:28-30` uses moving tags (`checkout@v7`, `setup-dotnet@v6`) without `persist-credentials: false`, unlike `release.yml`/`pages.yml`. Pin to the same SHAs, add `persist-credentials: false`. |
| A16 | TW5 | `BannedSymbols.txt` misses `Task<T>.WaitAsync(TimeSpan…)`, `PeriodicTimer(TimeSpan)`, `System.Threading.Timer`, `Thread.Sleep`, `SemaphoreSlim.Wait(Async)(TimeSpan)` and `TimeProvider.System`. No violation today; add them. |

## Part B – Structure and reuse

| # | IDs | Proposal | Effort |
|---|---|---|---|
| B1 | RD10 | `Battle.razor` writes the two-copy HP bar twice (fighter card `:42-46`, hit bar `:80-90`) and the index check twice (`FighterOf`, `TargetOf`) – the hidden „HP“ for screen readers already went into only one of them. `Shared/HpBar.razor` and one `FighterAt(battle, side, index)` helper. | S |
| B2 | RD11 | Public members only tests use: `OverlayServer.IsRunning` (A30 was solved by the port status), `SiteLinks.None`. Drop them; a shared test fake builds `new SiteLinks(new WebsiteOptions())`. | S |
| B3 | GL3 | `TrainerService.FindByNameAsync` is no longer called by production code (`ResolveNameAsync` replaced it), but the checklist and the Core CLAUDE.md still name it. Drop it, switch the tests, fix both docs. | S |

## Part C – Tests

TE proved the gaps by mutation: production code broken on purpose on a copy, the whole suite stayed green.

| # | IDs | Finding / proposal |
|---|---|---|
| C1 | TE1 | **M** The word „ja“ of the deletion confirmation (`TrainerRemoval.cs:45`) is not pinned: `confirms = true` stays green. A mod who repeats `!entfernen @ash` would delete without confirming. Test: twice without „ja“ and once with „nein“ → trainer still there, a second question went out. |
| C2 | TE2 | **M** No test checks that the game flows are atomic: removing `BeginTransactionAsync`/`CommitAsync` stays green in PvP, boss, NPC, rename and deletion. Each flow saves twice; a failing second save would leave the fight and the loot committed without outbox, achievement or cooldown, or a half-deleted trainer. One test per flow with an interceptor that throws on the second save. |
| C3 | TE3 | **M** Defaults only used through their symbol: `BossTargetDivisor` (the 1.8.0 value), `NpcWinCooldown`, `MaxRounds`, `CritChance`, `Damping`, `ChatText.MaxLength` (500, Twitch's limit), `HintInterval`, `CooldownReplyInterval`, `OfflineHintCooldown`, `BattleIndexSize`, `RecentBattlesPerTrainer`, the cache ages. One test `Defaults_match_spec_13` with literal values. |
| C4 | TE4 | **M** The A11 fix (refresh not cancelled by the caller, `TwitchTokenProvider.cs:89`) has no test: passing `ct` stays green. Hold the token endpoint, cancel the caller, release, assert the new token is stored. |
| C5 | TE5 | **M** C10 of review 1.0.3 holds only by timing: removing `_limiter.Record` in `ChatSender.cs:164` is red 3 of 5 times in the whole suite (`Assert.Single` right after `Eventually`). Assert on the fake clock: the second request only after `Advance(1 s)`. |
| C6 | TE6 | **M** The hit's page position (`SiteGenerator.cs:365`, 1.8.1) is only tested with fighter 0: a fixed fighter index stays green, so in a boss fight every hit on participant 2..N would draw against participant 1. Add a hit on side 0, fighter 1. |
| C7 | TE7 | **M** No test checks that a threshold is not reached one species early (`Achievements.cs:54`, `Distinct + 1` stays green); a wrong unlock is permanent. 14 distinct species must not unlock Eich. |
| C8 | SE2 | The deletion test does not assert that `statistik.json` (and `meta.json`) are queued; only `ForTrainers` puts them there today. Assert the full set of queued paths. |
| C9 | TE8 | The stop cap of the send queue (`ChatSender.cs:151`, 3 s instead of 60 s while stopping) has no test. |
| C10 | TE9, TE10, TE11 | Boss lobby branches: joining a lobby of the other mode before it ends (`BossEncounter.cs:66`); the late-join test cannot tell which join extended (`<=` → `<` at `:75` stays green); „Boss bereit“ after `!bosscooldown` without a pending announcement, e.g. after a restart (`:362`). One case each. |
| C11 | TE12 | Smaller unpinned branches, one case each: colour kept on a rename without colour (`TrainerService.cs:171`); a mod typing `!entfernen @x ja` at once is asked again forever if pending is not stored (`TrainerRemoval.cs:126`); per-NPC `Tokens`; catchers only on `BossDefeated`; `MaxBattleIdGap` boundary; `Attempts = 0` on re-queue; failed bucket listing; `OpenLive()` in `SitePublishWorker.cs:60` and `ProfileImageRefresher.cs:77`; chunked body over the size cap; `IsDefaultPort`; numeric id filter; withdrawn release (A5); rechecks stop once live; `vip` is not a mod. |
| C12 | TE13 | Web/UI: negative side, fighter or HP in a hit or log part (`Battle.razor:161-179`) are untested – a foreign file would throw instead of falling back; „low“ at exactly 25 % (`:43`, `:81`); `@key` on leaderboard rows (`Home.razor:36`); `Dispose` unsubscribing in UI components. Extend the theory `A_hit_the_page_cannot_draw_keeps_its_full_sentence` with negative values and a boundary case. |
| C13 | TE14 | Weak or hanging: `TrainerListTests.cs:144` and the busy test in `UpdateCheckTests.cs` (~250) read without a timeout and hang on a regression; fixed real-time waits before negative asserts in `UpdateCheckTests.cs:30,33` and `ProfileImageRefresherTests.cs:333-338`; `PumpUntilAsync` makes one test 1.6 s long. Bounded reads, `TimerCountingTimeProvider.ActiveTimers` instead of waits. |
| C14 | TE15 | C18 of review 1.0.3 only partly: second class `GitHubReleaseHintTests` in `UpdateCheckTests.cs:338`; `Chat/ChatTests.cs` tests EventSub; `UI.Tests/TrainerListTests.cs` holds backup, settings and shell tests; `Web.Tests/PageTests.cs` (655 lines) holds every page plus `Format`; fakes of UI.Tests and Web.Tests outside `Fakes/`. Split by page/service, move the fakes. |

**Redundant**

| # | IDs | Test(s) | Proposal | Catches the failure afterwards |
|---|---|---|---|---|
| C15 | TE | `FakeUpdater`/`FakeGate` in `UpdateCheckTests.cs:283/327` and `UpdateStatusTests.cs:98/115` | one shared copy linked into both projects | the same tests, e.g. `Installing_by_hand_works…`, `The_install_waits_while_a_boss_lobby_is_open` |

Not proposed: no test is redundant in a way that matters – each candidate TE checked catches a mutation of its own.

## Part D – Docs and CLAUDE.md

| # | IDs | Finding / proposal |
|---|---|---|
| D1 | RD1 | **M** `CLAUDE.md` is 18.0 KB (~4.5k tokens; review 1.0.3: 9.9 KB), all five together 39.8 KB (~10k). §3 is mostly the version table (fourth copy next to CHANGELOG, the work-packages table and the README), §9 "Next" is release notes back to 1.1.0 – and wrong for the next package: `:239` lists the `N = 4` dip as WP-09 work, which 1.8.0 closed (`roadmap.md:583`). Cut §3 to the invariants plus pointers, §9 to three or four lines, the status line to version plus next; in `/ship-wp` and `/release` say "replace" §9 instead of "update". Result ~10.5 KB. (D10 of review 1.0.3 asked for the same.) Effort S. |
| D2 | RD3 | **M** `README.md:11-15` says balancing „follows in 1.8.0“; the roadmap table (`:228-229`) ends at "1.8.0 … planned" and misses six releases. Replace the table with a pointer to CHANGELOG/releases plus WP-09 (1.9.0, proposal), fix the status. Effort S. |
| D3 | RD2 | **M** `docs/work-packages.md` is 73.9 KB, past the 50 KB trigger of the backlog item „one log file per package“; every review and package agent reads ~18k tokens to reach a few entries. Do the backlog item (index with an "Areas" column, one file per entry, links in skills and CLAUDE.md). Effort M. → Question 8. |
| D4 | RD4 | "Proposal" markers left on decided things: version 1.7.2/1.8.0 addenda (`work-packages.md:901-902, 942, 962`), `roadmap.md:557` (HP before and after, shipped), `roadmap.md:335` (raw chat colour in the dark theme, overruled by „die Balken-Farbe zum Standard“). Mark them decided; `/release` lists the proposal addenda of released packages and asks once. |
| D5 | RD5 | `branch-code-review/SKILL.md:51` says `feat/`/`fix/` branches get no protocol bullet, but every recent branch with a log entry has one and `/release` counts them. "Every branch with a log entry gets the protocol bullet". |
| D6 | RD6 | Pitfall files carry decision history: the three largest Web bullets (HP bar, „Schaden“ box, Tint) are mostly quotes and dropped variants (~2 KB); `Web/CLAUDE.md:31` „four items“ (five since 1.7.0); Core `:28`/`:69` say the same, `:55-61` keeps per-branch history (~0.5 KB). Keep rule plus source tag. |
| D7 | RD7 | `README.md:7-8` and `spec.md:4` still say the bot runs on the streamer's PC (outside the list in CLAUDE.md §8). Neutral: „on a Windows machine“. |
| D8 | RD8 | Stale status text: `roadmap.md:3-8` (intro), `:93` („Open: the `N = 4` dip“ vs `:583` closed), `:535-536` („WP-09 stays 1.8.0“), the "Open UX questions" section holds only decided items; `architecture.md:4-6` („WP-00 to WP-08“), `:31-33` (live check without the 2-min rechecks of A3). |
| D9 | WE6 | `spec.md:380` and the A33 comment in `MainLayout.razor:52-53` say the site deploys on merge and the bot follows with the streamer's next update; since 2026-11-10 it deploys on the tag, the bot updates itself after the stream. |

## Questions for the user

1. **Scope and order.** Proposal (default): everything in one package R2 on `wp/r2-review-1.8.1`, released as 1.8.2 (proposal) after the live test. A1 and A2 are the only ones with a real risk; A2 needs a change in the Cloudflare dashboard that only you can make – the docs and check steps come with R2. Alternatively A1 alone first as a patch.
2. **A1 overlay:** catch every bind error (default), or additionally switch the overlay off on the server (`Overlay:Enabled false` in `settings.json`), since it is not used?
3. **A2 Cloudflare:** WAF allowlist of the site's own path shapes plus a mandatory billing alert (default), or only the billing alert and accept the risk?
4. **A5 withdrawn release:** a successful check that finds nothing drops the downloaded update (default), or record "only a higher version stops it" in ADR-0010?
5. **A7 republish after an update:** automatically once on the first start after a version change (default), or only a hint in the card?
6. **A9 „Schlüssel entfernen“:** inline confirmation (default), or drop the button?
7. **A14 escaped boss:** the website and desktop get the chat's „– alle Pokémon wurden besiegt“ (default; needs „Alles neu veröffentlichen“, see A7), or record the difference as intended?
8. **D3 work-packages split:** now in R2 (default), or later?

## User decisions (2026-11-19)

Answer: „Alles mit Vorschlägen“

1. Everything in one package: R2 on `wp/r2-review-1.8.1`, version 1.8.2 (proposal, not confirmed) after the live test.
2. A1: catch every bind error; the overlay stays enabled.
3. A2: WAF allowlist of the site's own path shapes plus a mandatory R2 billing alert; the dashboard change is the user's.
4. A5: a successful check that finds nothing drops the downloaded update.
5. A7: republish once automatically on the first start after a version change.
6. A9: inline confirmation before removing the key.
7. A14: the website and desktop take the chat's „– alle Pokémon wurden besiegt“.
8. D3: split `docs/work-packages.md` now.
