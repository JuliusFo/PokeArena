# WP-01 — Twitch connection (2026-10-13, branch wp/01-twitch-connection)

- Built: own lean Twitch client in Core — device-code login for a public
  client, token provider with serialized refresh (single-use refresh tokens)
  and validation incl. scope check, Helix client (users, chat subscription,
  send), EventSub WebSocket session with keepalive timeout, `session_reconnect`
  handover and duplicate filter; `TwitchConnection` with automatic reconnect
  (2 s … 60 s), hourly validation and UI state; `ChatInbox` as the single
  serialized command queue, `CommandProcessor`, `ChatSender` (20 per 30 s,
  1 per second, 500-character split), `ChatLog`, dry run; `!pokearena` with a
  30 s cooldown (not in the dry run). Desktop: Twitch config, optional
  `appsettings.local.json`. UI: connection, dry-run and chat-log panels.
  Tests: 56 unit (fake HTTP, fake sockets, `FakeTimeProvider`), 7 bUnit, one
  skippable Twitch integration test; new `PokeArena.UI.Tests` in CI.
- User decisions: own client instead of TwitchLib, dry run, `!pokearena` as
  permanent status command (ADR-0002 addendum 2026-10-12); Twitch app
  registered as public client; test channel `ShiyoFurball`; client ID
  committed (public by design).
- Addenda: ADR-0002 (2026-10-12 decisions; 2026-10-13 grants, send rate, no
  token expiry in the UI, shared chat, channel setting). `AppInfo` moved to
  Core.
- Pitfalls: see `src/PokeArena.Core/CLAUDE.md` (parallel refresh logs the bot
  out; cancelling a WebSocket receive aborts the socket; shared chat; U+E0000
  suffix). Raw JSON in C# tests: `}}}` needs `$$$` raw strings or spaces.
- Live test 2026-10-13: bot account signed in via device code, `!pokearena`
  answered in `ShiyoFurball`; Twitch integration test green against the real
  API.
- Deliberately not done: detecting moderator status for a higher send rate,
  persisting the dry-run toggle (no DB before WP-02), channel setting in the
  UI (WP-08 setup question).
