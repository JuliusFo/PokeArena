# ADR-0002: Twitch integration

Status: accepted · Date: 2026-09-03

## Context

The bot reads and writes chat in one configured channel through a dedicated
bot account. The app is a desktop program, so it cannot keep a client secret.
On 2026-10-11 the user confirmed they will set up the bot account and its
tokens themselves; the app runs on another streamer's PC.

## Decision

- Read via **EventSub WebSocket** (`channel.chat.message`), write via **Helix
  Send Chat Message**. No IRC.
- Auth via **Device Code Flow** as a public client (no secret). Bot scopes
  `user:read:chat`, `user:write:chat`, `user:bot`; the broadcaster grants
  `channel:bot` or makes the bot a moderator (moderator also raises the send
  rate limit). Which of these grants is strictly required with a user token
  over WebSocket is verified in WP-01.
- `TwitchLib.Api` and `TwitchLib.EventSub.Websockets` (preview) are wrapped
  behind own interfaces so a hand-rolled client can replace them.
- Tokens are stored only in the Windows credential store; the UI shows their
  expiry dates.
- Outgoing messages go through a rate-limited queue; mass results (boss) are
  aggregated and respect the 500-character limit.

Deliberately **not**: IRC, an app access token (would need a client secret),
a hosted auth backend.

## Consequences

- Refresh tokens of public clients expire 30 days after they are generated
  (Twitch docs). The app must refresh regularly and fall back to a new device
  login on `400 Invalid refresh token`.
- Device Code Flow lets the user authorize the bot account from any device,
  e.g. their own phone, while the token lands on the streamer's PC.
