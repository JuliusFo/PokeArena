# Agent briefs (complete-code-review)

Each agent gets its block below verbatim, in addition to what `SKILL.md` step 2 lists for every prompt. The blocks say
what to check at least – an agent may go further within its topic. "Leave to" names the agent that owns a
neighbouring question, so a finding is reported once.

Common to all: read the files in question, never guess from names. A finding needs a failure scenario someone could
reproduce (input, steps, wrong result), not "could be a problem". Check the "Ohne Befund geprüft" list of the last
review and say whether it still holds for your topic. Decisions in `docs/work-packages.md`, `docs/roadmap.md`, ADRs
and items already in `docs/backlog.md` are not findings unless the code deviates from them.

## GL – Game logic and data

- `src/PokeArena.Core/Game/**`, `Core/Commands/**`, `Core/Data/**` (database, entities, catalogs, type chart,
  migrations and snapshot).
- Rules against `docs/spec.md`: every rule, every default of §13, every threshold; chat replies against
  `docs/chat-texts.md` (wording and cases such as unknown trainer, cooldown, self-challenge, own starter).
- Invariants: trainer by Twitch id; starter never transferred (loot, deletion, any new path); achievements never
  revoked; species by id; seed and full log per battle; one `BattleRandom(seed)` per encounter in a fixed order.
- Transactions: loot, deletion, battle result and `SiteOutbox` in one transaction; failures in between; desktop UI
  actions next to the command queue; dry-run switch during a lobby or challenge.
- Edge cases: trainer with only the starter, trainer deleted during an open challenge or lobby, double registration,
  renames (login vs display name), cooldowns across restarts, rounding in damage and boss scaling (ADR-0006), broken
  base data.
- Leave to: TW (queue, timers, Twitch), WE (site JSON, UI), SE (where input comes from), TE (tests), RD (duplicates,
  docs).

## TW – Twitch, chat queue, concurrency, host, build

- `Core/Twitch/**`: device code flow, `TwitchTokenProvider` (serialized refresh, single-use refresh token stored at
  once, 400 → new login), EventSub (keepalive, `session_reconnect` connects before closing, close/cancel order,
  connect timeouts), Helix (429/5xx, rate limit, 500 characters), `TwitchConnection` (states, which errors are final,
  reconnect backoff, live status from events vs. polling, standby, wrong account).
- `Core/Chat/**` and the command flow: one consumer of `ChatInbox`; timers post `QueuedWork`; own and shared-chat
  messages ignored; live gate; dry-run switch (`GameDatabase.Switched` on the UI thread) against running commands.
- Async: tokens passed on, no `.Result`, no `async void` except events (and `OnExit` is one), `Task.Delay(…,
  timeProvider, ct)`, lost fire-and-forget exceptions, which thread loops run on, shutdown order.
- `src/PokeArena.Desktop/**`: host setup, Serilog, credential store, backup, single instance, unhandled exceptions,
  overlay endpoint lifetime.
- Build and CI: `Directory.Build.props/.targets`, `Directory.Packages.props`, `BannedSymbols.txt` (also the overloads
  without `TimeProvider`), project references against `CLAUDE.md` §5, `.github/workflows/*` (token permissions,
  `persist-credentials`, pinned actions, version and CHANGELOG checks), `global.json`.
- Leave to: GL (rules), WE (publishing, UI), SE (security), TE (tests, packages), RD (duplicates, docs).

## WE – Publishing, website, overlay, desktop UI

- `Core/Publishing/**`: every write path a viewer could see queues its paths in the same transaction (search all
  writers in Game/Commands/UI); worker retry and order (does a full republish delay new fights?), R2 outage, partial
  success, restart, restoring a backup, key change; nothing from the dry run published; deleted trainers gone from
  every file.
- `src/PokeArena.Contracts/**` against the files generated and read: compatibility with files in R2, `meta.json`
  format version in both directions (site newer than bot is the normal case after a merge), nullability, time zones.
- `src/PokeArena.Web/**`: pages, loading and error states (404, network error, timeout, old format), GitHub Pages
  routing, guide page against spec §13 and `data/npcs.json`, CSS tokens light/dark/mobile, simple German.
- `src/PokeArena.UI/**` and `Desktop/Overlay/*`: event unsubscription, `InvokeAsync(StateHasChanged)`, render cost,
  streamer texts without technical words, tokens in `pokearena.css`, `@@`, local times; overlay when the app or OBS
  starts first, port in use, polling cost.
- Leave to: GL, TW, SE (XSS, R2 keys, privacy in JSON), TE, RD.

## SE – Security and privacy

- Follow the checklist's section "Security and privacy" for every source listed there (chat messages and arguments,
  odd Unicode and whitespace, long input, floods; Twitch identity and badges; hand-edited files; R2 data in the WASM
  app), each traced to every sink (chat output incl. echo of arguments and mentions, moderator actions, website and
  overlay, R2 keys, deletion incl. log file and privacy page, secrets, loopback endpoint, size and load incl. the
  send queue). Name source → boundary check → sink → the test that fails without the check.
- Also: OAuth scopes and token storage, R2 credentials, HTTP clients, deserialization of outside files, logging of chat
  content, Cloudflare rules in `docs/website-setup.md` (cache key, rate limit – cost attacks on R2), git history
  (`git log --all -p -S <word>`) for tokens and player data.
- Leave to: nobody – a security question is always reported here, also when it touches another topic.

## TE – Tests

Only TE builds and runs tests, in the snapshot: `dotnet build PokeArena.slnx -warnaserror`, then
`tests/PokeArena.Core.Tests`, `tests/PokeArena.UI.Tests`, `tests/PokeArena.Web.Tests` (also under other cultures and
in parallel mode); never the integration tests (read them and check that they skip cleanly).
`dotnet list PokeArena.slnx package --vulnerable --include-transitive` and `--outdated`.

**Gaps – prove them by mutation.** Copy Core and Core.Tests into a separate scratch folder (never edit the snapshot),
break one guard at a time, build, run the whole suite, restore. A gap is "mutation X stays green". At least: every
invariant guard (starter, achievements, trainer by id, outbox in the transaction, live gate, moderator access of the
real commands, deletion confirmation, dry-run separation, refresh serialization, reconnect order, publish worker),
every fix since the last review (protocols in `docs/work-packages.md`, CHANGELOG), and the changed defaults.

**Weak and flaky:** asserts that cannot fail (text that never appears, a mock returning what it was told, a fake that
answers synchronously in a concurrency test), fixed waits, real clocks, culture or order dependence, battles predicted
by retrying instead of by seed, tests that compute the expected value with the code under test; the ten slowest tests.

**Redundant:** same arrange with one changed value (→ `[Theory]`), same failure on the same or a lower layer, tests of
the language or a library. Per proposal the test that still catches the failure. Never propose removing a test that
pins a fix or an invariant unless another test covers exactly that failure.

**Structure:** test folders mirror the code, one class per file, fakes and helpers in `Fakes/` instead of copies.

- Leave to: RD (duplicate production code). A production bug seen on the way is reported with the prefix
  "(Produktion)".

## RD – Reuse, dead code, structure, docs

- Duplicates and copies that drift (battle texts in chat/site/desktop, cooldowns, duration formatting, team setup,
  enums and records in Data vs Contracts, CSS between desktop and site), helpers that exist but are not used.
- Dead code: unused types, members, config keys, CSS classes, JS functions, packages; public API only for tests
  (confirm by grep over src, tests, tools incl. Razor, CSS, HTML).
- Readability and structure: long methods, wrong names and comments, files in the wrong folder or layer, `tools/`
  using hard-coded copies of defaults.
- Docs: README (install steps for the streamer, building), `CLAUDE.md` and `src/*/CLAUDE.md` (still true, size in KB
  and tokens, what could go), `.claude/skills/*` (paths, commands, files exist and are committed – check
  `git check-ignore`), spec against the code (§13 defaults, commands), `docs/chat-texts.md` against every text in the
  code in both directions, architecture, ADRs (numbering, status, addenda), roadmap and backlog (done items, open
  questions already answered), work-package logs, CHANGELOG sections = tags, `docs/website-setup.md` against the code,
  relative links.
- Leave to: TE (duplicate tests); functional bugs only briefly with the prefix "(Fehler)".
