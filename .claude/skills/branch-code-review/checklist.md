# Review checklist (PokeArena)

Hand this file to every reviewer agent; it lists what is specific to this project. General review craft (logic errors,
edge cases, resource leaks) applies on top. The pitfalls in `src/<Project>/CLAUDE.md` hold the details; check against
them.

## Severity

- **H** (high) wrong or lost game data (specimens, achievements, battle logs), a broken invariant, a security or
  privacy hole, the bot hangs, answers twice or stops answering in chat
- **M** (medium) wrong behaviour in realistic cases (a command, a fight result, a stale website), or a clear
  maintenance burden
- **L** (low) minor

Reviews up to 1.2.0 used S/M/K for the same three levels (PR descriptions, commit messages such as "review
feat/boss-lobby, K1, K3", the original review 1.0.3).

## Invariants (`CLAUDE.md` §3)

- A trainer is found by Twitch user id, never by display name; names are only looked up (`FindByNameAsync`: login
  first, then display name) and never used as a key, file name or foreign key.
- SQLite is the only source of truth: no state that exists only in a JSON file, in memory across restarts (except the
  documented short-lived lobby and challenges) or in the website.
- Randomness and time only via the injected interfaces (`TimeProvider`, the random source); one `BattleRandom(seed)`
  per encounter in a fixed order; every battle stores seed and full log.
- Species base data by id; no stats, names or types copied into the DB.
- Achievements are never revoked; the starter specimen is never transferred (PvP loot, deletion, any new path).
- Loot transfers and deletions run in one transaction, together with their `SiteOutbox` entries.
- `PokeArena.Core` has no UI or Windows dependency; Web references only Contracts; layer rules as in `CLAUDE.md` §5.
- No official Pokémon assets anywhere, not even hot-linked (ADR-0008).

## Correctness

- Command flow: everything goes through `ChatInbox` → `CommandProcessor`, one at a time. Timers never touch game state
  directly; they post `QueuedWork`. No second consumer, no locks added to work around one.
- Dry run: services that cache data reset on `GameDatabase.Switched`; in-memory state remembers `IsSimulated` or a
  generation and is dropped after a switch; nothing from the sandbox is published (`OpenLive()` only in the publish
  worker and backups).
- Live gate: new commands respect `StreamStatus.AcceptsCommands`; mod commands bypass it on purpose (WP-08).
- Async: `CancellationToken` passed on; nothing blocks on `.Result`; timers via `Task.Delay(…, timeProvider, ct)`;
  components re-render via `InvokeAsync(StateHasChanged)` and unsubscribe in `Dispose`.
- Twitch: token refresh only through `TwitchTokenProvider.RefreshAsync`; reconnect connects before closing; own
  messages and shared-chat messages ignored; replies respect the 500-character limit and the rate limiter.
- Database: `GameDatabase.Open()` per unit of work, no cached context; model change = new migration; `DateTimeOffset`
  as UTC ticks.
- Website data: every change a viewer could see queues its paths in the same transaction (`SiteOutbox.ForBattle` /
  `ForTrainers`); Contracts changes are compatible with files already in R2 (or `meta.json` format version bumped and
  "republish everything" noted).

## Security and privacy: follow outside input to where it lands

A fixed pass, not a general impression: for every source the change touches, trace the value to every sink it
reaches.

Sources (untrusted):
- Chat messages: command arguments (starter, NPC and species names, `@mentions`), odd Unicode (U+E0000, zero-width,
  RTL), very long input, message floods.
- Twitch identity data: display names and logins (may contain characters that mean something in Markdown, HTML or
  chat), badges and roles that decide who is a moderator.
- Files: `settings.json` and `appsettings*.json` edited by hand, backups, `data/*.json`.
- Website data from R2 as read by the WASM app (anyone who gets the R2 key can write it).

Sinks (never unchecked):
- Chat output: names and arguments echoed into bot messages (no way to make the bot write `/` commands or mention
  everyone), length limits, aggregation for mass events.
- Moderator actions (`!bossreset`, `!entfernen`): the role check uses `ChatMessage.IsModerator` (from the EventSub
  badges of that message), never a name or a list of names; confirmation as specified.
- Website and overlay: no `MarkupString` or raw HTML with names or log text; R2 object keys built from the Twitch id,
  never from a name; nothing private in the JSON (tokens, internal ids beyond the documented ones, data of deleted
  trainers).
- Deletion: the trainer's file deleted from R2, names in stored logs replaced, all affected files republished; nothing
  of the trainer left in any website file, the log file or the overlay.
- Secrets: tokens and R2 keys only in the Credential Manager, never logged, never in exceptions or message boxes.
- Local endpoints: the overlay listens on loopback only.
- Size and load: limits for input and lists, no unbounded loops or retries (reconnect, publish worker), the chat cannot
  make the bot spam.
- Git: no player data, tokens or other people's channel names in code, tests, sample data (`wwwroot/beispieldaten/`)
  or commits. The developer's own test channel and the project's own domains are fine (review 1.0.3).

A security finding names the concrete path from source to sink, the check at the boundary and a test that fails
without the fix.

## Texts and UI

- Chat and website texts in simple German for casual viewers; desktop texts for the streamer without technical words.
- A changed chat text is changed in `docs/chat-texts.md` in the same change.
- A changed default of `docs/spec.md` §13 or an NPC threshold also updates the guide page (`Pages/Guide.razor`).
- Colours via the tokens in `pokearena.css` (desktop) and the site's CSS; light and dark.
- Literal `@` in Razor written as `@@`; times shown local (desktop) or Europe/Berlin (website).

## Tests

- Core logic has unit tests without external dependencies (`FakeTimeProvider`, seeded randomness); infrastructure in
  `PokeArena.Integration.Tests`, skipping cleanly when it is missing.
- A bug fix comes with a test that fails without it, where feasible.
- New `CommandProcessor` tests report the stream live first.
- Tests that predict battles do so via the seed, not by retrying until a result appears.

## Docs

- `docs/spec.md` (game rules, commands, defaults), `docs/architecture.md`, ADRs and the `src/<Project>/CLAUDE.md`
  pitfalls still true after the change.
- Package log in `docs/work-packages.md`, `CHANGELOG.md` entry under `[Unreleased]` for anything players or the
  streamer notice.

## Not a finding

- Decisions the user already made: "User decisions" in `docs/work-packages.md`, `docs/roadmap.md`, ADRs and their
  addenda.
- Items already in `docs/backlog.md` or `docs/roadmap.md` (e.g. WP-09 balancing), unless the change makes them worse.
- Features of later packages that are deliberately missing.
- Style preferences without a concrete cost.
