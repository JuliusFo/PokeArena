# Architecture

Gameplay rules live in [spec.md](spec.md); this file describes how the code is
organised around them. Status: WP-01 (Twitch, chat queue, commands) and
WP-02 (database, base data, registration) built; the rest is planned.

## Core modules (`PokeArena.Core`)

| Folder | Responsibility |
|---|---|
| `Twitch/` | Device-code auth and tokens, Helix client, EventSub WebSocket session, `TwitchConnection` (login, reconnects, state for the UI) |
| `Chat/` | `ChatInbox` (single serialized queue of incoming messages), `ChatSender` (rate limit, splitting, dry run), `ChatLog` for the UI |
| `Commands/` | Parsing, routing, permission and cooldown checks, dialog states (open challenges, lobby) |
| `Game/` | `TrainerService` (registration, display names, trainer list); later battle engine, boss, PvP, NPC fights, achievements |
| `Data/` | EF Core model (`PokeArenaDb`), migrations, `GameDatabase` (live database or dry-run sandbox), `SpeciesCatalog` (validated `data/pokemon.json`) |
| `Publishing/` | `ISitePublisher`, R2 implementation, outbox worker |

External systems sit behind seams with fakes for tests: HTTP via `HttpClient`
(fake handler), the EventSub socket via `IEventSubSocketFactory`, outgoing chat
via `IChatSender`, later `ISitePublisher`. Incoming messages from Twitch and
from the dry run both go into `ChatInbox`; the single `CommandProcessor`
consumes it, so game commands never run concurrently. In the dry run
("Probelauf") replies only go to the chat log, game data goes to a fresh
in-memory sandbox database, and real chat messages are logged but not
processed (WP-02 decision).

## Data model

Built in WP-02: `Trainer` and `Specimen`. The rest is a sketch for later
packages. Timestamps are stored as UTC ticks (SQLite cannot compare
`DateTimeOffset`); enums as text. The live database is
`%LOCALAPPDATA%\PokeArena\pokearena.db` (WAL), migrated at startup.

| Entity | Fields | Notes |
|---|---|---|
| `Trainer` | TwitchUserId (PK), DisplayName, RegisteredAt | Display name refreshed when seen |
| `Specimen` | Id, TrainerId, SpeciesId, CaughtAt, Origin, IsStarter | PvP loot changes `TrainerId`; unique filtered index: one starter per trainer |
| `Battle` | Id, Kind, StartedAt, Seed, WinnerRef, LogJson, TransferredSpecimenId? | Log stored verbatim (ADR-0004) |
| `BattleParticipant` | BattleId, TrainerId/NpcId, TeamJson | |
| `AchievementUnlock` | TrainerId, Threshold, UnlockedAt | Never deleted except with the trainer |
| `Cooldown` | Key, Until | Boss global, failed-lobby retry, per trainer+NPC |
| `OpenChallenge` | ChallengerId, TargetId, ExpiresAt | One per trainer, either direction |
| `PublishQueueItem` | Id, Path, Payload, State, Attempts | Outbox for R2 uploads |

Species, type chart and NPC teams stay in `data/*.json`, referenced by id.
Runtime balancing values (spec §13) are stored in the DB and editable in the UI.

## Flows

- **Boss** (spec §4): `!boss` opens a lobby → countdown → participant check →
  tier draw by participant count → fight resolved instantly → catches,
  achievements, publish, cooldown.
- **PvP** (spec §6): `!fight @x` creates an `OpenChallenge`; the same command
  from the target accepts → random teams of 6 → fight → loot transfer in one
  transaction.
- **NPC** (spec §7): `!challenge <NPC>` → unlock and cooldown check → fight
  against the fixed team with strength multiplier.
- **Publishing** (spec §9): game logic only enqueues `PublishQueueItem`s; a
  background worker uploads to R2 and retries on failure. "Republish
  everything" regenerates all files from the DB.

## Desktop UI concept (`PokeArena.Desktop` + `PokeArena.UI`)

Blazor Hybrid (ADR-0007): Desktop is a thin WPF host with a `BlazorWebView`;
all screens are Razor components in `PokeArena.UI` with CSS design tokens.
Operated by the streamer, so every screen must be usable without technical
knowledge. Areas:

- Built (WP-01), area "Bot": connection (device-code login with the code shown
  large, connect/disconnect, sign out, status with a plain-German reason),
  dry run (toggle plus a field to type commands as a simulated viewer) and the
  live chat log (newest first, in memory, last 500 entries).
- Built (WP-02), area "Trainer": read-only list (name, starter, number of
  Pokemon, registration date) with search; tabs in the top bar, a
  "Probelauf aktiv" badge while the dry run is on.
- Planned: manual corrections and deletion of trainers; balancing config
  editor (all values from spec §13, live); operations (backup now, republish
  everything); local Kestrel endpoint serving the OBS overlay.

## Keyboard shortcuts

None defined yet.
