# PokeArena.Core — pitfalls

- `net10.0` only: no WPF, WinForms, registry or other Windows APIs here. Windows-specific
  implementations (e.g. the Credential Manager) live in Desktop behind a Core interface.
- Time and randomness: `DateTime.Now/UtcNow/Today`, `DateTimeOffset.Now/UtcNow`, `new Random()` and
  `Random.Shared` break the build (`BannedSymbols.txt`, ADR-0004). Inject `TimeProvider`; tests use
  `FakeTimeProvider`. Seeded `new Random(seed)` is allowed — only inside the injected random source.
- Twitch (WP-01): EventSub WebSocket sends keepalives; missing ones mean reconnect. `session_reconnect`
  must connect to the new URL **before** closing the old socket, or messages are lost. Public-client refresh
  tokens are single-use (store each new one at once) and expire after 30 days of inactivity; on
  `400 Invalid refresh token` fall back to a new device login.
- Refreshes go only through `TwitchTokenProvider.RefreshAsync` (serialized). A second, parallel refresh spends
  the single-use token twice and logs the bot out. Never log tokens.
- Cancelling a pending `ClientWebSocket.ReceiveAsync` aborts the socket — close politely first, then cancel
  (see `EventSubSession.Connection.DisposeAsync`).
- Every incoming message goes through `ChatInbox`; only `CommandProcessor` reads it. Commands therefore run one
  at a time and may keep state (e.g. cooldowns) without locks — don't add a second consumer.
- Chat events: ignore the bot's own messages (reply loops) and shared-chat messages from other channels
  (`source_broadcaster_user_id`). Some clients append U+E0000 to repeated messages; `ParsedCommand` strips it.
- Timers in async loops: `Task.Delay(delay, timeProvider, ct)` and `new CancellationTokenSource(timeout,
  timeProvider)`, so tests drive them with `FakeTimeProvider.Advance`. The overloads without a `TimeProvider`
  (and `Stopwatch`, `CancelAfter`) break the build since review 1.0.3.
- SQLite (WP-02/08): copying the database file while it is open loses the WAL content — back up with
  `VACUUM INTO` or the SQLite backup API.
- The `boss` cooldown row is shared by a fight's cooldown and the failed-lobby retry; only `Cooldown.Length` (null for the
  retry) tells them apart and gives the length to shrink from — never derive it from `Until` or the current settings
  (review feat/boss-cooldown-reduced).
- Database access only through `GameDatabase.Open()` (one short-lived context per unit of work, `await using`):
  it picks the live database or the dry-run sandbox. Never cache a `PokeArenaDb` or a connection string.
- `DateTimeOffset` is stored as UTC ticks (`ConfigureConventions`); SQLite cannot compare or sort it otherwise.
  Values read back always have offset zero.
- Model change → new migration, never edit an applied one:
  `dotnet tool restore`, then
  `dotnet ef migrations add <Name> --project src/PokeArena.Core --startup-project src/PokeArena.Core --output-dir Data/Migrations`.
  The test `Migrations_cover_the_whole_model` fails if one is missing.
- Game services that cache data (e.g. `TrainerService`'s registration cache) must reset on
  `GameDatabase.Switched` — the dry run swaps the whole database underneath them.
- Timed game steps (WP-03, e.g. the end of a boss countdown) must not touch game state from the timer: wait with
  `Task.Delay(…, timeProvider, ct)`, then post a `QueuedWork` to `ChatInbox` and do the work there. State that
  belongs to one mode (an open lobby) remembers `IsSimulated` and is dropped when the dry run was switched.
- Battles: draw everything of one encounter from one `BattleRandom(seed)`, in a fixed order (boss, fielded
  species or team specimens, hits). Changing the order changes what a seed means — fine, logs are data (ADR-0004), but tests that predict
  the boss with `BossDraw.Species(…, new BattleRandom(seed))` rely on the boss being drawn first.
- In-memory game state tied to one database (WP-04, open PvP challenges) must not be mutated from
  `GameDatabase.Switched` other than by a generation counter (`Interlocked`); drop stale entries on the command
  queue (see `PvpService`). The UI switches the dry run with `DryRunMode.SwitchOnQueue`, so `Switched` fires
  between two commands (review 1.0.3).
- Chat mentions (`@name`) resolve by `Trainer.Login` first, then by display name (`TrainerService.FindByNameAsync`);
  the bot's own replies should mention the login when the viewer has to type it (`!fight @login`).
- Website (WP-06): every change a viewer could see on the site queues its paths with `SiteOutbox.EnqueueAsync` in
  the **same** context/transaction as the change (`SiteOutbox.ForBattle` / `ForTrainers`). Forgetting it leaves
  the site stale until "republish everything". The battle id exists only after the first `SaveChangesAsync`.
- The publish worker reads `GameDatabase.OpenLive()` — the one exception to `Open()`; the sandbox is never published.
- Live gate (WP-07): `CommandProcessor` drops real messages while `StreamStatus.AcceptsCommands` is false — new
  `CommandProcessor` tests must report the stream live first. Stream events only report changes: whenever a new
  EventSub session starts, ask `HelixClient.IsLiveAsync` **after** subscribing, or a change in between is lost.
- Backups (WP-08): `DatabaseBackup` uses `VACUUM INTO` on `OpenLive()`. From an in-memory database it writes the
  copy into memory (memdb VFS), so its tests run on a real file in Integration.Tests.
- Error and hint replies to one viewer (unknown name, too few Pokémon, cooldown …) go through
  `IChatSender.SendHint(viewerId, command, …)`, throttled per viewer and command (not in the dry run); fight results and announcements use `Send`. A name a
  viewer typed is repeated only through `ChatText.Echo` (review 1.0.3).
- A fight's result is worded twice: the chat message when it happens, and `BattleTexts.Describe` (fight page, website
  and desktop lists) from the stored log. Information added to one belongs in the other; share the sentence via a
  `BattleTexts` helper (feat/boss-lobby).
- Twitch: only 403 (and a missing channel or a wrong account) stops the connection for good; every other refusal
  is retried with backoff. A Helix `GET /streams` answer never overrides a stream event of the last 10 minutes
  (review 1.0.3).
