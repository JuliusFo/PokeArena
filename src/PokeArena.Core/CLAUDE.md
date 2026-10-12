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
- SQLite (WP-02/07): copying the database file while it is open loses the WAL content — back up with
  `VACUUM INTO` or the SQLite backup API.
