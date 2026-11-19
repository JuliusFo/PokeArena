# WP-07 — Live status (2026-11-03, branch wp/07-live-status)

- Built: `StreamStatus` (live state + in-memory „Auch ohne Stream“ switch);
  `TwitchConnection` subscribes to `stream.online` / `stream.offline` next to
  the chat and asks Helix `GET /streams` after subscribing and with the hourly
  token check; changes go to the chat log. `CommandProcessor` drops real
  messages while offline; known commands get one hint per 5 minutes, the dry
  run is never gated. Twitch card shows „Stream: live / offline“ and the
  switch. 5 new tests (connection, gate, card); 218 tests green.
- User decisions: own package before operations (now WP-08, versions shift:
  0.7.0 here, 1.0.0 stays operations); offline hint with a global cooldown;
  the switch is not stored.
- Pitfalls: Core (gate in `CommandProcessor` tests, ask the live state only
  after subscribing).
- Live test 2026-11-03 passed (user, test channel): go live / end stream,
  offline hint with cooldown, „Auch ohne Stream“ switch. Released as 0.7.0.
