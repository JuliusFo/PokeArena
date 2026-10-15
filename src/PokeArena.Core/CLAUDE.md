# PokeArena.Core — pitfalls

- `net10.0` only: no WPF, WinForms, registry or other Windows APIs here. Windows-specific
  implementations (e.g. the Credential Manager) live in Desktop behind a Core interface.
- Time and randomness: `DateTime.Now/UtcNow/Today`, `DateTimeOffset.Now/UtcNow`, `new Random()` and
  `Random.Shared` break the build (`BannedSymbols.txt`, ADR-0004). Inject `TimeProvider`; tests use
  `FakeTimeProvider`. Seeded `new Random(seed)` is allowed — only inside the injected random source.
- `data/pokemon.json` is generated: change `tools/GeneratePokemonData.cs`, rerun it, never edit the JSON.
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
  timeProvider)`, so tests drive them with `FakeTimeProvider.Advance`.
- SQLite (WP-02/07): copying the database file while it is open loses the WAL content — back up with
  `VACUUM INTO` or the SQLite backup API.
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
- Battles: draw everything of one encounter from one `BattleRandom(seed)`, in a fixed order (boss, specimens,
  hits). Changing the order changes what a seed means — fine, logs are data (ADR-0004), but tests that predict
  the boss with `BossDraw.Species(…, new BattleRandom(seed))` rely on the boss being drawn first.
- `tools/*.cs` file-based apps default to AOT, which disables reflection-based `System.Text.Json` and breaks the
  base data loaders: keep `#:property PublishAot=false` next to `#:project`.
