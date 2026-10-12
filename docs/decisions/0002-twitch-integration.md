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

- Refresh tokens of public clients are single-use and expire after 30 days of
  inactivity (corrected 2026-10-12, see addendum). The app must store every
  new refresh token at once and fall back to a new device
  login on `400 Invalid refresh token`.
- Device Code Flow lets the user authorize the bot account from any device,
  e.g. their own phone, while the token lands on the streamer's PC.

## Addendum 2026-10-12 (WP-01 planning)

- **Own lean client instead of TwitchLib** (user decision): `HttpClient`,
  `ClientWebSocket` and `System.Text.Json`, still behind `IChatTransport` /
  `IChatSender`. WP-01 needs only a handful of endpoints (device flow, token
  refresh and validation, users, EventSub subscription, send chat message)
  and one EventSub message type; `TwitchLib.EventSub.Websockets` is a
  preview, and reconnect behaviour stays fully under our control and
  testable with fakes. The interface boundary above already allowed this.
- **Refresh tokens, corrected** (Twitch docs checked 2026-10-12): refresh
  tokens of public clients are **single-use** — every refresh returns a new
  one that must be stored immediately — and expire after **30 days of
  inactivity**, not 30 days after they were generated. While polling the
  device flow, handle `authorization_pending` (keep polling) and
  `invalid device code` (expired: start over); `slow_down` is not
  documented.
- **Twitch app** registered 2026-10-12 by the user as a **Public** client,
  category Chat Bot. The redirect URL `http://localhost` is required by the
  console but unused by the device flow. The client ID is not a secret and
  lives in `appsettings.json`.
- `!pokearena` (spec §8) is the WP-01 demo command and stays as a permanent
  status check.

## Addendum 2026-10-13 (WP-01 implementation)

- **Grants** (Twitch chat docs checked 2026-10-12): with a *user* access token
  over EventSub WebSocket, reading needs only `user:read:chat` and sending
  only `user:write:chat` of the bot account; `user:bot` / `channel:bot` only
  matter for an app access token (chat bot badge). The bot still requests
  `user:bot` so a later switch needs no new login. Neither moderator status
  nor `channel:bot` is required — moderator only raises the send limit and
  protects against slow mode. Live test 2026-10-13: reading and replying work
  with the user token; the bot was a moderator in the test channel, so the
  case without moderator status is still untested.
- **Send rate**: the sender always keeps the normal-user limits (20 messages
  per 30 s, 1 per second) instead of detecting moderator status — results are
  aggregated, so this is far from tight.
- **Token expiry in the UI** is dropped: access tokens last 4 h and are
  refreshed automatically, refresh tokens have no fixed date. The UI shows the
  signed-in bot account instead; a dead login surfaces as "sign in again".
- **Shared chat**: messages that originate in another channel of a shared
  chat session are ignored — their viewers do not play here. The bot's own
  messages are ignored too.
- **Channel setting**: `PokeArena:Twitch:Channel`; during development it comes
  from a git-ignored `appsettings.local.json`. How the streamer sets it is the
  open setup question of WP-07.
