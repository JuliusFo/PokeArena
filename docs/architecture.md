# Architecture

Gameplay rules live in [spec.md](spec.md); this file describes how the code is
organised around them. Status: specification phase — nothing here is built yet.

## Core modules (`PokeArena.Core`)

| Folder | Responsibility |
|---|---|
| `Twitch/` | EventSub WebSocket client, Helix sender, device-code auth, outgoing rate limiter |
| `Commands/` | Parsing, routing, permission and cooldown checks, dialog states (open challenges, lobby) |
| `Game/` | Battle engine, boss encounter, PvP, NPC fights, achievements |
| `Data/` | EF Core model, SQLite, migrations, base-data loader (`data/*.json`) |
| `Publishing/` | `ISitePublisher`, R2 implementation, outbox worker |

External systems sit behind interfaces with fakes for tests:
`IChatTransport`, `IChatSender`, `ISitePublisher`. The desktop app offers a
"dry run" mode using the fakes.

## Data model (sketch)

| Entity | Fields | Notes |
|---|---|---|
| `Trainer` | TwitchUserId (PK), DisplayName, RegisteredAt | Display name refreshed when seen |
| `Specimen` | Id, TrainerId, SpeciesId, CaughtAt, Origin, IsStarter | PvP loot changes `TrainerId` |
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

## Desktop UI concept (`PokeArena.Desktop`)

Operated by the streamer, so every screen must be usable without technical
knowledge. Planned areas:

- Connection: Twitch device-code login, connect/disconnect, status, token
  expiry dates.
- Live chat log.
- Trainer list with manual corrections and deletion.
- Balancing config editor (all values from spec §13, live).
- Operations: backup now, republish everything, dry-run toggle.
- Local Kestrel endpoint serving the OBS overlay.

## Keyboard shortcuts

None defined yet.
