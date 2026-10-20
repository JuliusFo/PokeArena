# Architecture

Gameplay rules live in [spec.md](spec.md); this file describes how the code is
organised around them. Status: WP-01 (Twitch, chat queue, commands), WP-02
(database, base data, registration), WP-03 (battle engine, boss,
achievements) and WP-04 (PvP) built; the rest is planned.

## Core modules (`PokeArena.Core`)

| Folder | Responsibility |
|---|---|
| `Twitch/` | Device-code auth and tokens, Helix client, EventSub WebSocket session, `TwitchConnection` (login, reconnects, state for the UI) |
| `Chat/` | `ChatInbox` (single serialized queue of incoming messages and `QueuedWork`), `ChatSender` (rate limit, splitting, dry run), `ChatLog` for the UI |
| `Commands/` | Parsing, routing, permission and cooldown checks |
| `Game/` | `TrainerService` (registration, display names, trainer list), `BossEncounter` (lobby, fight, catches, cooldown), `Achievements`, `BattleService` (battle list for the UI), `PvpService` (open challenges, fight, loot transfer); later NPC fights |
| `Game/Battles/` | Pure engine without database: `DamageCalculator`, `BossBattle`, `TeamBattle` (1-vs-1 with teams, for PvP/NPC), `BossDraw`, `BattleLog` (structured events, JSON, German lines), `BattleSettings` / `BossSettings` / `PvpSettings`, `BattleRandom` + `ISeedSource` |
| `Data/` | EF Core model (`PokeArenaDb`), migrations, `GameDatabase` (live database or dry-run sandbox), `SpeciesCatalog`, `TypeChart`, `NpcCatalog` (validated `data/*.json`) |
| `Publishing/` | `ISitePublisher`, R2 implementation, outbox worker |

External systems sit behind seams with fakes for tests: HTTP via `HttpClient`
(fake handler), the EventSub socket via `IEventSubSocketFactory`, outgoing chat
via `IChatSender`, battle seeds via `ISeedSource`, later `ISitePublisher`.
Incoming messages from Twitch and from the dry run both go into `ChatInbox`;
the single `CommandProcessor` consumes it, so game commands never run
concurrently. Timed game steps (the end of a boss countdown) post a
`QueuedWork` into the same queue instead of running on a timer thread. In the
dry run ("Probelauf") replies only go to the chat log, game data goes to a
fresh in-memory sandbox database, and real chat messages are logged but not
processed (WP-02 decision).

## Data model

Built in WP-02: `Trainer` and `Specimen`; in WP-03: `Battle`,
`BattleParticipant`, `AchievementUnlock`, `Cooldown`; in WP-04: `Trainer.Login` and the
PvP loot columns of `Battle`. The rest is a sketch for
later packages. Timestamps are stored as UTC ticks (SQLite cannot compare
`DateTimeOffset`); enums as text. The live database is
`%LOCALAPPDATA%\PokeArena\pokearena.db` (WAL), migrated at startup.

| Entity | Fields | Notes |
|---|---|---|
| `Trainer` | TwitchUserId (PK), DisplayName, Login, RegisteredAt | Display name and login refreshed when seen; `@name` mentions resolve by login, then display name |
| `Specimen` | Id, TrainerId, SpeciesId, CaughtAt, Origin (Starter, Boss), IsStarter | PvP loot changes `TrainerId`; unique filtered index: one starter per trainer |
| `Battle` | Id, Kind, FoughtAt, Seed, BossSpeciesId?, LootSpecimenId?, LootSpeciesId?, Outcome, Rounds, LogJson | Log stored verbatim (ADR-0004); fighters, owners and HP are in the log. PvP: outcome `ChallengerWon` / `OpponentWon`, loot without foreign key |
| `BattleParticipant` | Id, BattleId, TrainerId, Won | Boss: `Won` = caught the boss; PvP: `Won` = winner. Bosses and NPCs need no row, they are in the log |
| `AchievementUnlock` | TrainerId + Threshold (PK), UnlockedAt | Never deleted except with the trainer |
| `Cooldown` | Key (PK), Until | `boss` (global cooldown and failed-lobby retry share it); later per trainer+NPC |
| `PublishQueueItem` | Id, Path, Payload, State, Attempts | Outbox for R2 uploads |

Species, type chart and NPC teams stay in `data/*.json`, referenced by id (NPC
teams by slug). Runtime balancing values (spec §13) are planned in the DB and
editable in the UI; until then `BattleSettings` / `BossSettings` hold the spec
defaults in code.

## Flows

- **Boss** (spec §4, built): `!boss` opens a lobby (in memory, lost on restart)
  → countdown → `QueuedWork` closes it on the command queue → participant check
  → one seed per encounter draws tier, boss species, each participant's
  specimen and all hits → `BossBattle` → battle, catches, achievements and
  cooldown in one transaction → chat messages. Later: publish.
- **PvP** (spec §6, built): `!fight @x` resolves `@x` by login or display name and
  opens a challenge in memory (expires lazily, dropped on a database switch);
  the same command from the target accepts → requirements checked again → one
  seed draws both teams of 6 (challenger first), all hits and the loot →
  `TeamBattle` → battle, loot transfer and the winner's achievements in one
  transaction → chat messages. Later: publish.
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
- Built (WP-03), area "Kämpfe": the latest 50 fights (time, opponent,
  trainers, result); clicking one shows its course as German log lines. WP-04
  adds PvP fights („Ash gegen Misty“, winner and loot).
- Built (WP-04), dry run: a button tops the typed test viewer up to a full PvP
  team (`DryRunTrainers`, queued work, sandbox only).
- Planned: manual corrections and deletion of trainers; balancing config
  editor (all values from spec §13, live); operations (backup now, republish
  everything); local Kestrel endpoint serving the OBS overlay.

## Keyboard shortcuts

None defined yet.
