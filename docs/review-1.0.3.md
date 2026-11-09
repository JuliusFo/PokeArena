# Whole-solution review 1.0.3 (2026-11-08)

Request (2026-11-08): „Mach mal ein Code-Review auf unseren bisherigen Code. Du kannst dabei ja die Reviews von FerretSharp als Anleitung nehmen, musst sie aber nicht 1:1 übernehmen.“ First review of the whole solution (WP-00 to WP-08, releases up to 1.0.3), modelled on FerretSharp's `complete-code-review`, scaled down to six parallel agents by topic. All agents only read, on a fixed copy of `a2fdf4f` (= `main`):

- GL: game logic and data
- TW: Twitch, chat queue, concurrency, host, build/CI
- WE: publishing, website, overlay, desktop UI
- SE: security and privacy
- TE: tests; the only agent that builds and runs tests, plus mutation tests on a copy
- RD: reuse, dead code, structure, docs

**Status:** implemented with R1 (`docs/work-packages.md`, branch `wp/r1-review-1.0.3`), released in 1.1.0; not reproduced A31, not done C22, open in the backlog: A8's local file, A33 (N-1), connection test in the settings.

Duplicate findings are merged (all IDs named). **Confirmed** means: additionally checked by hand in the code; otherwise the agent's assessment applies.

Severity (as in `.claude/skills/branch-code-review/checklist.md`; this review originally used S/M/K): **H** wrong or lost game data, broken invariant, security or privacy hole, bot hangs, answers twice or not at all · **M** wrong behaviour in realistic cases or a clear maintenance burden · **L** minor.

Build and tests on the snapshot:
- `dotnet build -warnaserror` without warnings.
- 233 tests green: Core 192, UI 30, Web 11. Also under de, en-US, tr-TR and invariant culture, and in 8 parallel runs.
- Integration tests only read. Coverage: no package referenced.
- No vulnerable packages; only patch levels are outdated (EF Core/Extensions 10.0.14, AWSSDK.S3, WebView.Wpf, CredentialManager, bunit, TimeProvider.Testing).

## Checked without findings (excerpt)

- **Invariants:**
  - Keys only by Twitch id, R2 paths only from the numeric id.
  - No species values in the DB.
  - No randomness and no clock in Core outside the interfaces. Exception: one real-time CTS, see A23.
  - Starter never loot: filter plus filtered unique index.
  - Achievements stay when a species is lost.
  - One `BattleRandom(seed)` per encounter in a fixed order; seed and full log stored.
- **Game rules:**
  - Boss: tiers, HP 1.5 × N, targets ⌈N/3⌉, catchers, cooldowns, „Boss bereit“.
  - Damage formula, team fight, PvP, NPC: checked against spec §4–§7 and ADR-0006.
  - All 17 defaults of §13 match the code, as does `npcs.json` with §7 and the guide page.
- **Transactions:**
  - Boss, PvP, NPC and deletion run in `BEGIN IMMEDIATE`, together with the outbox.
  - Deletion removes everything (incl. the prefix edge case of the NPC cooldowns), anonymizes the logs and queues all affected files.
  - Deletion from the UI runs through the queue.
- **Twitch:**
  - Refresh serialized and stored at once.
  - `session_reconnect` connects first, then closes the old connection.
  - Keepalive via `TimeProvider`.
  - Own messages and shared chat ignored, U+E0000 removed.
  - Frames limited to 1 MB, duplicate filter.
  - Backoff 2–60 s.
  - `ChatText.Split` keeps to 500 characters.
- **Command flow:**
  - One reader of `ChatInbox`; timers only post `QueuedWork`.
  - Live gate before the first message; mod commands bypass it on purpose.
- **Security:**
  - Mod role only from the badges of the message.
  - Deletion confirmation: same mod, same id, same mode, 60 s.
  - No `MarkupString`, overlay only via `textContent`, Kestrel only on loopback.
  - Secrets only in the Credential Manager and never logged; EF Core without SQL logging.
  - No tokens or player data in the git history.
- **Publishing:**
  - A revision field catches re-queueing during an upload.
  - Backoff bounded, a restart loses nothing.
  - `OpenLive()` only in the worker, republish and backup.
  - Cache headers 10 s / 1 day, 404 not cached.
- **Structure and docs:**
  - Layer rules §5 kept.
  - Central package versions, `nuget.config` only nuget.org.
  - ADR 0001–0009 without gaps.
  - All relative links resolve, CHANGELOG sections = tags.
  - All CSS classes used, backlog without done items.
  - `docs/chat-texts.md` complete in both directions, except D8.
- **Tests:**
  - No fixed waits except C12, no real clock.
  - Fights predicted by seed.
  - No test slower than 0.1 s, except the warm-up per assembly.

## Part A – Bugs and security

### High

None.

### Medium

| # | IDs | Location | Finding | Certainty |
|---|---|---|---|---|
| A1 | TW4 | `Core/Twitch/TwitchConnection.cs:283`, `Core/Twitch/EventSub/ChatMessageEvent.cs:24` | **A login with the streamer's instead of the bot's account is not detected.**<br>Scenario: on the streamer's PC twitch.tv is signed in with their account. During the device login (first setup or after 30 days of inactivity) they therefore confirm with their own account. Then `BotUserId == BroadcasterId`.<br>Consequences: every message of the streamer is dropped as the bot's "own message"; they can neither play nor use `!bossreset`/`!entfernen`. The bot writes as the streamer. The UI shows „Verbunden“.<br>Proposal: in `RunSessionAsync` stop when `bot.UserId == channel.Id`, with a new `TwitchProblem.WrongAccount` („Bitte mit dem Bot-Account anmelden“). Plus a test. | confirmed |
| A2 | TW2 | `TwitchConnection.cs:246-253` | **Every non-transient `TwitchApiException` (4xx except 429) ends the connection for good.** The state is then `Failed`, without a new attempt.<br>Scenario: after standby or a network loss the subscriptions take longer than the 10 s Twitch allows. Twitch closes the session, the running `POST eventsub/subscriptions` gets 400. The bot stays offline in the middle of the stream until the streamer clicks „Erneut versuchen“.<br>Proposal: treat only 401, 403 and "channel not found" as final; retry all other 4xx with the existing backoff. | path confirmed, trigger (status code of the dead session) suspected |
| A3 | TW1 | `TwitchConnection.cs:330, 398` | **The live status from Helix `GET /streams` overwrites the previous status every time.**<br>Scenario: PokeArena starts (or reconnects) in the first minutes after going live. `stream.online` came before the subscription, and Helix in practice still reports "offline". Consequence: for an hour the bot drops all commands and answers „läuft nur, solange der Stream live ist“. Conversely, after the stream ends it keeps accepting commands for up to an hour.<br>Proposal: an event of the last ~10 min takes precedence over the poll. After a start with "offline" ask every 2 min for a few minutes (or generally every 5 min instead of hourly). Test with `FakeTimeProvider`. → Question 2. | path confirmed, Helix delay suspected |
| A4 | TW3, TW5 | `Desktop/App.xaml.cs:123-135` | **`async void OnExit`: after the first `await` WPF ends the dispatcher, the continuation never runs.** So on exit `_host.Dispose`, `Log.CloseAndFlushAsync`, draining the send queue and closing the socket cleanly are all missing.<br>Scenario: the streamer closes the window right after a boss fight; result and achievement messages are lost. The data stays intact.<br>Proposal: in the window's `Closing` set `e.Cancel = true`, `await` stopping the host, then `Shutdown()`.<br>Caution (TW5): because of `Task.Yield` the Twitch loop runs on the UI thread. A blocking wait would therefore deadlock. So at the same time use `Task.Run` instead of `Task.Yield` and honour the stop token in `TwitchStartup.StopAsync`. | mechanism confirmed, WPF behaviour suspected |
| A5 | SE1 | `Core/Chat/ChatSender.cs:36`, error replies in `CommandProcessor.cs:105`, `PvpService.cs:79/85`, `NpcService.cs:47-50`, `RegisterCommand.cs:54` | **A chat flood backs up important bot messages.** The send queue is unbounded. Error and hint replies are not throttled per user; only `!pokearena`, `!arena`, the boss cooldown and the offline hint are.<br>Scenario: two viewers send 20 `!fight`/`!challenge xyz` each per 30 s. That makes 40 replies per 30 s, but the limiter manages only 20. After 5 min of spam boss results are 5 min late. The 2-min deadline of `!fight` runs out before the challenged viewer sees the message.<br>Throttling is in the backlog, its effect on important messages is not.<br>Proposal: throttle error and hint replies per user (one per 30 s), with `ReplyThrottle` from B3. Drop hints with a maximum age in the queue, never results. Test: 20× `!fight` in 10 s → at most one reply. → Question 3. | confirmed |
| A6 | WE1 | `Core/Publishing/SiteOutbox.cs:45-50`, `README.md:91-97` | **„Alles neu veröffentlichen“ after restoring a backup does not clean up R2.** The README promises it does. Only paths the DB knows are queued.<br>Scenario: backup from yesterday, today trainer X and fights 121–130 were added. Consequences:<br>– `trainer/X.json` and `kampf/121…130.json` stay in R2.<br>– X is no longer in the DB, so `!entfernen` cannot delete them either. Their page stays public (privacy).<br>– The next fight gets id 121 again. Cloudflare and the browser serve the old fight 121 for a day, the chat link shows the wrong fight.<br>Proposal: on republish also list `trainer/` and `kampf/` in the bucket (ListObjectsV2) and queue them; the worker deletes paths without a source by itself. After restoring, set the fight sequence from the highest uploaded id, or say so in the README. → Question 4. | confirmed |
| A7 | WE2, WE6 | `SitePublishWorker.cs` (`OrderBy(NextAttemptAt)`), `SiteOutbox.cs:33/49`, `Web/Pages/Battle.razor:72` | **Republishing holds up new fights.** All rows get `NextAttemptAt = T0`, a later fight `T1 > T0` and is therefore sorted to the end.<br>Estimate: with ~300 trainers and 1000 fights that is ~52 rounds of 25 files, so several minutes. The fight page gives up after 2 min and reports „Diesen Kampf gibt es nicht“ (WE6: also on plain network errors). The chat link is then broken.<br>Proposal: process due rows with `Attempts == 0` by `QueuedAt` descending. The fight page tells "not there yet" from "error". | confirmed |
| A8 | WE3 | `UI/OverlayPanel.razor:125-127`, `Desktop/Services/OverlayServer.cs:46` | **If OBS starts before PokeArena, the overlay stays empty.** The app itself serves the overlay page; the browser source gets "Connection refused" and does not reload by itself. The retry in `overlay.html` only works once the page has loaded. So the overlay stays empty for the whole stream.<br>Proposal: at least a hint in the card („PokeArena vor OBS starten, sonst die Quelle aktualisieren“, or the OBS option "Refresh browser when scene becomes active"). More robust: a local HTML file that polls via CORS. → Question 5. | suspected (OBS/CEF behaviour) |
| A9 | SE5 | `docs/website-setup.md:38-56` | **The cache rule does not ignore the query string.** `rangliste.json?r=<random>` is then always a MISS and a paid R2 read. The rate limit allows ~5/s per IP permanently, i.e. ~13 M reads per month. That is exactly what the cache should prevent according to the ADR-0003 addendum ("no spending cap").<br>Proposal: ignore the query string in the cache key, plus a WAF rule "query string not empty or method not GET/HEAD → block". Check step in §5: `curl -I …?x=1` twice → `HIT` or 403. | docs confirmed; live configuration not checkable |

### Low

| # | IDs | Finding |
|---|---|---|
| A10 | TW8 | `ChatSender.cs:71` drops messages while there is no Twitch target (reconnect, 2–60 s). A boss result in that window never reaches the chat. Wait for a target for a bounded time (e.g. 60 s); a message never sent cannot arrive twice. |
| A11 | TW7 | `TwitchTokenProvider.cs:87` cancels the refresh with the caller's token. If Twitch has already executed the refresh, the new single-use token is lost, and the streamer needs a new device login. Run the request with its own timeout and without the caller's cancellation. (suspected) |
| A12 | TW6 | `EventSubSocket.cs:30`: `ConnectAsync` has no time limit. During the handover it blocks the session loop including the keepalive check. If the upgrade hangs, the bot hangs indefinitely. Linked CTS with ~15 s via `timeProvider`. (suspected) |
| A13 | GL1, TW9 | The dry-run switch flips on the UI thread, right between two `database.Open()` of one command. A simulated `!registrieren` can thus land in the live DB together with the outbox. The window is milliseconds wide. Switch as `QueuedWork` through the inbox. |
| A14 | GL2, SE6 | `TrainerService.FindByNameAsync` with `FirstOrDefault` over a non-unique `Login`.<br>Scenario: A was called "bob", renamed and no longer writes; B later takes "bob". `!entfernen @bob ja` can delete A.<br>When storing a login, clear the same login on other trainers; with several matches in chat, do not delete. |
| A15 | GL3 | Deleting a trainer leaves their open challenges in memory. B gets „du hast schon eine offene Herausforderung“ until the timeout. Call `PvpService.Forget(trainerId)` from the deletion. |
| A16 | GL4 | `!challenge @Eich` → „einen NPC namens @Eich gibt es nicht“. `!fight` strips the `@`, `!challenge` does not. |
| A17 | GL5 | `SpeciesCatalog.Validate` does not check that every rarity tier has species. If one is missing, every lobby with that tier throws; the chat only shows „Ein wilder Boss nähert sich!“. Missing `typen`/`team` give a `NullReferenceException` instead of the readable message. |
| A18 | SE2 | `PvpService.cs:76/85`, `NpcService.cs:39/47` echo the argument unshortened. Splitting happens only at U+0020, so with NBSP a "word" can be a whole sentence of 480 characters. That costs twice the send budget, and the echo stays when a mod deletes the message. Mention ping: suspected. Echo only for the login pattern `^[A-Za-z0-9_]{1,25}$`, split at `char.IsWhiteSpace`. |
| A19 | SE3 | `ChatMessageEvent.cs:49` only recognizes the badge `moderator`. Twitch's "Lead Moderator" probably carries `lead_moderator` and cannot use `!entfernen`/`!bossreset`. Fail-closed. (suspected – check the badge names against the Twitch docs) |
| A20 | SE4, RD | The log file (debug, 14 days) keeps display name and login on renames (`TrainerService.cs:138`) and the full message text on errors (`CommandProcessor.cs:113`), even after `!entfernen`. The privacy page (`Web/Pages/Privacy.razor:10`) names neither the log nor login, badges and tokens. Log only ids, extend the page. |
| A21 | SE7, TW11 | `release.yml`: `contents: write` for the whole job, `actions/checkout` puts the token into `.git/config`. Package code in build and tests could use it to replace the release ZIP. `persist-credentials: false`, upload in a job of its own; optionally SHA pins. |
| A22 | TW10 | `release.yml` checks neither that tag and `<Version>` match nor that the CHANGELOG section exists (`exit 0` → generated notes). An early step should fail in both cases. |
| A23 | TW12 | `BannedSymbols.txt` does not ban `Task.Delay`, `CancellationTokenSource(TimeSpan)`, `CancelAfter` and `Stopwatch` without `TimeProvider`. There already is one violation: `EventSubSession.cs:228`. Add them and switch over. |
| A24 | TW13 | `global.json` `rollForward: latestFeature` plus `AnalysisLevel latest` plus warnings as errors: a new SDK on the runner can turn CI and release red without a code change. Pin `AnalysisLevel` to `10.0`. (suspected) |
| A25 | WE4 | `SiteOutbox.EnqueueAsync` changes existing rows as tracked entities. `RegisterAsync` and `ObserveAsync` run without a transaction. If the worker deletes the row exactly in between, a `DbUpdateConcurrencyException` follows: the registration is lost, the chat gets no reply. Wrap both in `BeginTransactionAsync`. |
| A26 | WE5 | After correcting a wrong R2 key the rows keep their backoff (up to 15 min). The card still shows „Zugangsdaten abgelehnt“, and the new key looks wrong. In addition, `SaveCredentials` disposes the publisher in the middle of an upload. On `R2Access.Changed` reset the waiting times, dispose the old publisher only after the round. |
| A27 | WE7 | `Home.razor:12-15`, `Battles.razor:66-69`: if the first load fails, „Noch niemand dabei“ stands right next to the error hint. Empty state only when `!Failed`. |
| A28 | WE8 | `PollingPage.cs:101-121` only catches `HttpRequestException`. An HttpClient timeout (`TaskCanceledException`) silently ends the polling or leaves the page at „Lädt …“. |
| A29 | WE9 | `overlay.html:207`: „1 Arten“, in the first stream for all five entries. |
| A30 | WE10 | If port 5299 is in use, only the log says so; the overlay card still gives the instructions. Show the status. |
| A31 | WE11 | `TrainerService.DeleteAsync` does not notify `BattleService`; the „Kämpfe“ tab keeps showing the deleted name. |
| A32 | WE12 | Every overlay request (every 2 s while OBS runs) loads all trainers with all specimens. Cache the result until `Changed`. (suspected – not measured) |
| A33 | WE13 | `MainLayout.razor:254` only checks "data newer than app". The normal case is the reverse, though: Pages deploys on merge, the bot only with the next ZIP. On the first format change the new site then reads old files for days. Read N-1 or show a hint; record the order in the `/release` skill. → Question 6. |
| A34 | WE14 | `Home.razor:19` „Stand 22:14 Uhr“ without a date; two days later it looks like today. |

## Part B – Structure and reuse

| # | IDs | Proposal | Effort |
|---|---|---|---|
| B1 | RD1 | **Fight texts once:** headline and result are produced three times (chat in Boss/Pvp/NpcService, website in `SiteGenerator.Describe`, desktop in `BattleService` + `BattleListPanel`) and have already drifted apart:<br>– NPC first win: three different sentences.<br>– Deleted trainers: „(gelöscht)“ vs „[gelöscht]“.<br>– The desktop shows today's names, the website the names at the time of the fight.<br>– `TierSuffix` exists twice (UI copy, because it is `internal`), the token plural three times.<br>Proposal: one class `BattleTexts` in Core, used by website and desktop. | M |
| B2 | RD2 | Two `BattleKind` (Data/Contracts) with a mapping switch and a CLAUDE.md trap of its own, two `BattleSummary` with different content. Use `Contracts.BattleKind` in the DB (enum as text, same values), rename the Core `BattleSummary`. | S |
| B3 | RD3 | Reading/setting a cooldown twice identically (`BossEncounter`, `NpcService`), "reply at most every X" four times. New: `Cooldowns.Get/SetAsync` and `ReplyThrottle` (basis for A5). | S |
| B4 | RD4, GL6 | German duration formatting four times. `NpcService.Duration` and `HoursAndMinutes` give different texts for 90 min; after WP-09 the loss message and the cooldown reply contradict each other. One `ChatText.Duration`, drop the duplicate `<summary>` in `NpcService.cs:198`. | S |
| B5 | RD5 | Team draw, fighter creation, `Rounds = (Attacks+1)/2` and the achievement announcement exist two or three times. "Dry run?" is decided in three ways (`lobby.IsSimulated`, `message.IsSimulated`, `_database.IsSandbox`). NPC team size from `PvpSettings`. | S |
| B6 | RD6 | `tools/BalancingSimulator.cs:117-125` hard-copies the defaults – of all tools the one for WP-09. Read them from `BattleSettings.Default`/`BossSettings.Default`. | S |
| B7 | RD7 | Dead or only for tests: `BattleRandom.Seed`, `SiteLinks.None`, `OverlayServer.IsRunning` (used with A30), `Format.Count` (one call with identical singular/plural), fallback `"daten/"` in `Web/Program.cs:11`, `PackageVersion Microsoft.Extensions.Hosting` without a user. | S |
| B8 | RD8 | Wrong comments: `PollingPage.cs:6` (overlay, 15 s), `site.css:1` ("same palette" – not in dark mode), `AppPaths.cs:4` ("later backups"), balancing comments in `PokeArenaOptions.cs:5`/`BattleSettings.cs:5` (see D3). | S |
| B9 | RD9 | Web: `BattleList` and `SpeciesName` live in `Layout/`, they belong in `Shared/`. The hint „Daten konnten nicht geladen werden …“ exists four times; the CTS/dispose pattern is copied between the battle and the trainer page. | S |

## Part C – Tests

TE proved the gaps by mutation: production code broken on purpose, the whole suite stayed green. This happened on a copy of its own; the snapshot stayed unchanged.

| # | IDs | Finding / proposal |
|---|---|---|
| C1 | TE1 | **M** (guards an H trap) `TwitchAuthTests.cs:217` `Concurrent_refreshes_…` cannot fail: the fake answers synchronously. Without `_refreshLock` everything stays green – exactly the bug that, according to the Core CLAUDE.md, logs the bot out. Block the fake with a `TaskCompletionSource` until a second call waits. |
| C2 | TE2 | **M** If `pending.TrainerId == target…` or `pending.IsSimulated == …` disappears from `TrainerRemoval.cs:45-46`, everything stays green. Then `!entfernen @ash` followed by `!entfernen @misty ja` deletes Misty without asking. Add two cases. |
| C3 | TE3 | **M** No test checks the `Access` of the real commands. `!entfernen`/`!bossreset` set to `Everyone` → everything green, every viewer can delete. A `[Theory]` over all `IChatCommand` with the expected access, plus a processor test with the real `RemoveCommand` from a viewer. (confirmed: no test references the two commands) |
| C4 | TE4 | **M** Outbox entries for PvP, NPC and rename untested; only boss, registration and deletion check the queue. |
| C5 | TE5 | **M** `Republishing_queues_every_file` runs without a fight; removing the fight pages from `EnqueueEverythingAsync` stays green. |
| C6 | TE6 | **M** The order "live status only after the subscriptions" (Core CLAUDE.md) is untested. |
| C7 | TE7 | **M** `BossEncounterTests.cs:189` `DoesNotContain("AV", …)` cannot fail. Duplicate achievement announcements go unnoticed, also for PvP and NPC. |
| C8 | TE8 | **M** `BossDraw` is untested: HP without `× N`, swapped tiers and a missing cap stay green. The boss test computes the expected value with `BossDraw` itself; the HP test still uses 1.8. So no test pins the change from 1.0.1 (1.5). |
| C9 | TE9 | **M** „Boss bereit“: the conditions open lobby, dry run and `!= until` can be removed without a test turning red. |
| C10 | TE10 | **M** `ChatSender`: split and limiter untested inside the sender (only the units). |
| C11 | TE11 | **M** `SiteGenerator`: only the PvP page is checked. Catchers from the log (fix from WP-08), NPC texts and the overlay top 5 untested. |
| C12 | TE12 | **M** `EventSubSessionTests.cs:59-76`: real `Task.Delay(50)`. The test proves neither the grace period nor that a keepalive restarts the timer; on a slow machine it can hang (suspected). Rewrite with a gate instead of a wait. |
| C13 | TE13 | Starter never loot: the test catches the bug only with seed 6 of 8. Make it deterministic (TeamSize 2, loser has starter + 1). |
| C14 | TE14, TE15 | More branches without a hit: cache reset after deletion, resetting `failures`, deleted lobby participant, `ArenaCommand`, round limit on a tie, `authorization_revoked`, 403 → `MissingPermission`, achievement stays after a PvP loss. |
| C15 | TE16 | `NpcServiceTests.The_dry_run_skips_the_cooldown` sends simulated messages to the live DB and does not test the real dry run. |
| C16 | TE17 | The NPC table of the guide page is hard-coded; the test only checks the count, the first and the last entry. Compare against `data/npcs.json`. |
| C17 | TE18 | No architecture test: "Web only → Contracts", "no official assets" (image files and sprite hosts in `wwwroot`/`*.razor`). |
| C18 | TE19 | Structure: `RecordingSender` copied five times, `FixedSeeds` four times, `FakeTwitchConnection` twice → `Fakes/`. Test folders do not mirror the code (`Chat/ChatTests` tests EventSub, `TrainerListTests` contains Shell/Settings/Backup). |
| C19 | TE20 | `Integration.Tests/TwitchLoginTests.cs:280` counts as "read only" according to its comment, but deletes the real `PokeArena:twitch-token` on an invalid login. Fix the comment or save and restore. |

**Redundant**

| # | IDs | Test(s) | Proposal | Catches the failure afterwards |
|---|---|---|---|---|
| C20 | TE | `CommandTests.Status_command_answers_with_the_version_then_cools_down` (:41) | merge into the alias test | `Version_runs_the_status_command_and_shares_its_cooldown` (:65) |
| C21 | TE | `ChatTests` split cases (:45-65, three facts) | one `[Theory]` | the same cases as InlineData |
| C22 | TE | `SitePublishWorkerTests.Unexpected_errors_get_a_generic_reason` (:188) | theory with the reason part of `A_failed_upload_backs_off_…` | the theory; the backoff part stays a test of its own |

Not proposed: removing `The_starter_is_never_the_loot`. It pins an invariant and is only replaced (C13).

## Part D – Docs and CLAUDE.md

| # | IDs | Finding / proposal |
|---|---|---|
| D1 | RD10 | **M** The `/release` skill was never committed: `.gitignore:25` `[Rr]elease/` matches `.claude/skills/release/`. Commit a2fdf4f only contains `branch-code-review`, although its message names both. `CLAUDE.md` §1 points to a skill no clone has. Add `!.claude/skills/release/` and commit. (confirmed) |
| D2 | RD12 | **M** The moderator question contradicts itself:<br>– `CLAUDE.md` §8: "`channel:bot` or moderator required".<br>– ADR-0002 addendum: not required with a user token, moderator only protects against slow mode; the path without moderator is untested.<br>– spec §12: moderator "for the higher rate limit", which the limiter deliberately does not use.<br>– README: the step is missing from the streamer's installation guide.<br>This affects the upcoming installation. Add "make the bot a moderator" to the README, align the three places. (confirmed) |
| D3 | RD11, GL6 | **M** Five places say "balancing in the DB / runtime editor planned": spec §10, `architecture.md:57-60`, ADR-0001, `PokeArenaOptions.cs:5`, `BattleSettings.cs:5`. That contradicts the decision of 2026-11-04 (code defaults, no editor, spec §13). ADR-0001 also says challenges live in the DB. Correct them, ADR-0001 with an addendum. |
| D4 | RD14 | `spec.md` outdated:<br>– §14 chat wording still OPEN.<br>– §6 "until the website exists".<br>– §7 "drafts".<br>– §9 overlay as a website page, 15-s polling, "CDN, once a custom domain is used".<br>– §5 example log lines.<br>– §8 switch name differs from the UI („Auch ohne Stream“). |
| D5 | RD15 | Outdated states:<br>– `architecture.md:4-7` ("WP-01…WP-06 built"), `:41`, `:140` (placeholder shortcuts).<br>– `README.md:11` (1.0.0), README "Building" without Web/Integration.Tests.<br>– `roadmap.md:30-31` (broken list), `:284` (answered UX question), `:220` (connection test of the settings not built and not noted as a deviation – suspected).<br>– ADR-0002 addendum "open setup question of WP-08". |
| D6 | RD13 | `CHANGELOG.md:171`: orphaned fragment at the end of [0.1.0]. |
| D7 | RD16 | `website-setup.md` §5 promises that the R2 test runs. The test however reads the bucket only from `appsettings*.json`, not from `settings.json`. Add a step or let the test read `settings.json`. |
| D8 | RD17 | `chat-texts.md:62,64`: row 31 only knows „{n} Tokens“ (the code has 0/1/n); row 33 lacks the dry-run variant. |
| D9 | RD18 | The checklist forbids "real channel names". Tests and docs use the own test channel and `arena(-data).cloudfox.xyz`; every reviewer reports that again. Make the rule precise. → Question 7. |
| D10 | RD19 | Size: `CLAUDE.md` 9.9 KB (~2.5k tokens), all five together ~21 KB (~5.3k).<br>Shorten:<br>– version table §3 (third copy).<br>– Core:8/53 (duplicate of the root).<br>– Core:41-42 (tools AOT is stated in the tools files).<br>– Core:52 (goes away with B2).<br>– merge Desktop:8-9/24-25, Desktop:26.<br>Add: `docs/chat-texts.md` and the review documents in §1, Web in line 18. |

## Questions for the user

1. **Scope and order.** Proposal:
   - **Before the installation at the streamer's** as patch 1.0.4: D1, D2, A1, A2, A4, A10, A19, A29 and the test gaps C1–C3. That is first setup, connection, shutdown and mod rights.
   - Afterwards a review package 1.1.0 before or together with WP-09: the rest of A, C and D, plus B. B1/B4/B6 touch WP-09 directly.
   - Or everything in one package?
2. **A3 live status:** check every 5 min (simple), or event precedence plus polling after the start (more accurate)?
3. **A5 throttling:** this pulls the backlog item "Throttle repeated cooldown/error replies per user" forward. One error reply per viewer and 30 s – agreed?
4. **A6 restoring a backup:** list and clean up the bucket on republish (M) or only extend the README (fight ids then remain a risk)?
5. **A8 overlay:** only a hint in the card, or a local HTML file that polls via CORS?
6. **A33 format version:** be able to read N-1, or a hint „Daten werden umgestellt“? Only needed at the first format change – into the backlog as well?
7. **D9:** allow the test channel and the own domains in tests/docs (make the checklist precise) or replace them neutrally?
8. This document and future whole-solution reviews: record them as a skill `complete-code-review` for PokeArena?

## User decisions (2026-11-08)

Answer: „Alles in einem Paket. halte den Ablauf als Skill fest.“

1. Everything in one package: R1 on `wp/r1-review-1.0.3`.
2.–7. Left open. The simpler or proposed path is implemented in each case (`CLAUDE.md` §7):
   - **A3:** the hourly poll does not overwrite a status an event set in the last 10 min. After a start with "offline", ask every 2 min for 10 min.
   - **A5:** one error or hint reply per viewer and 30 s. The backlog item goes away with it.
   - **A6:** on republish list and clean up the bucket, raise the fight sequence above the highest id in the bucket.
   - **A8:** only the hint in the overlay card. The local HTML file goes to the backlog.
   - **A33:** a hint on the website for an older data format. Reading N-1 goes to the backlog, the order into the `/release` skill.
   - **D9:** allow the own test channel and the own domains; the checklist is made precise.
8. Record the process as a skill: `.claude/skills/complete-code-review/` (`SKILL.md`, `agents.md`).
