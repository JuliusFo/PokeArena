# Architecture

Gameplay rules live in [spec.md](spec.md); this file describes how the code is
organised around them. Status: WP-01 (Twitch, chat queue, commands), WP-02
(database, base data, registration), WP-03 (battle engine, boss,
achievements), WP-04 (PvP), WP-05 (NPC fights) and WP-06 (website, overlay)
built; the rest is planned.

## Core modules (`PokeArena.Core`)

| Folder | Responsibility |
|---|---|
| `Twitch/` | Device-code auth and tokens, Helix client, EventSub WebSocket session, `TwitchConnection` (login, reconnects, state for the UI) |
| `Chat/` | `ChatInbox` (single serialized queue of incoming messages and `QueuedWork`), `ChatSender` (rate limit, splitting, dry run), `ChatLog` for the UI |
| `Commands/` | Parsing, routing, permission and cooldown checks |
| `Game/` | `TrainerService` (registration, display names, trainer list), `BossEncounter` (lobby, fight, catches, cooldown), `Achievements`, `BattleService` (battle list for the UI), `PvpService` (open challenges, fight, loot transfer), `NpcService` (NPC fights, rewards, cooldowns) |
| `Game/Battles/` | Pure engine without database: `DamageCalculator`, `BossBattle`, `TeamBattle` (1-vs-1 with teams, for PvP/NPC), `BossDraw`, `BattleLog` (structured events, JSON, German lines), `BattleSettings` / `BossSettings` / `PvpSettings` / `NpcSettings`, `BattleRandom` + `ISeedSource` |
| `Data/` | EF Core model (`PokeArenaDb`), migrations, `GameDatabase` (live database or dry-run sandbox), `SpeciesCatalog`, `TypeChart`, `NpcCatalog` (validated `data/*.json`) |
| `Publishing/` | `SiteOutbox` (queue paths in the game transaction), `SiteGenerator` (renders every site file from the DB), `SitePublishWorker` (uploads, backoff, status for the UI), `ISitePublisher` + `R2SitePublisher` / `R2Access` (AWSSDK.S3, key in the secret store), `SiteLinks` (battle links in chat), `OverlayOptions` |

External systems sit behind seams with fakes for tests: HTTP via `HttpClient`
(fake handler), the EventSub socket via `IEventSubSocketFactory`, outgoing chat
via `IChatSender`, battle seeds via `ISeedSource`, site uploads via `ISitePublisherFactory`.
Incoming messages from Twitch and from the dry run both go into `ChatInbox`;
the single `CommandProcessor` consumes it, so game commands never run
concurrently. Timed game steps (the end of a boss countdown) post a
`QueuedWork` into the same queue instead of running on a timer thread. In the
dry run ("Probelauf") replies only go to the chat log, game data goes to a
fresh in-memory sandbox database, and real chat messages are logged but not
processed (WP-02 decision). Outside the dry run `CommandProcessor` also asks
`StreamStatus` whether the channel is live (WP-07): `TwitchConnection` keeps it
current from `stream.online` / `stream.offline` on the same EventSub session
plus Helix `GET /streams` on every new session and with the hourly token
check; the streamer's „Auch ohne Stream“ switch overrides it in memory.

## Data model

Built in WP-02: `Trainer` and `Specimen`; in WP-03: `Battle`,
`BattleParticipant`, `AchievementUnlock`, `Cooldown`; in WP-04: `Trainer.Login` and the
PvP loot columns of `Battle`; in WP-05: `Trainer.Tokens`, `Battle.NpcId` /
`Tokens` and `NpcBadge`; in WP-06: `PublishQueueItem` and `Specimen.AcquiredAt`. The rest is a sketch for
later packages. Timestamps are stored as UTC ticks (SQLite cannot compare
`DateTimeOffset`); enums as text. The live database is
`%LOCALAPPDATA%\PokeArena\pokearena.db` (WAL), migrated at startup.

| Entity | Fields | Notes |
|---|---|---|
| `Trainer` | TwitchUserId (PK), DisplayName, Login, RegisteredAt, Tokens | Display name and login refreshed when seen; `@name` mentions resolve by login, then display name. Tokens: plain balance, the first win against an NPC adds its tokens |
| `Specimen` | Id, TrainerId, SpeciesId, CaughtAt, AcquiredAt, Origin (Starter, Boss, Npc), IsStarter | PvP loot changes `TrainerId`; unique filtered index: one starter per trainer; `AcquiredAt` = when the current owner got it (leaderboard ties) |
| `Battle` | Id, Kind, FoughtAt, Seed, BossSpeciesId?, NpcId?, LootSpecimenId?, LootSpeciesId?, Tokens?, Outcome, Rounds, LogJson | Log stored verbatim (ADR-0004); fighters, owners and HP are in the log. PvP: outcome `ChallengerWon` / `OpponentWon`, loot without foreign key. NPC: the trainer is the challenger, loot = reward specimen of a first win, `Tokens` = tokens of a first win |
| `BattleParticipant` | Id, BattleId, TrainerId, Won | Boss: `Won` = caught the boss; PvP and NPC: `Won` = winner. Bosses and NPCs need no row, they are in the log |
| `AchievementUnlock` | TrainerId + Threshold (PK), UnlockedAt | Never deleted except with the trainer |
| `NpcBadge` | TrainerId + NpcId (PK), WonAt, BattleId | First win against an NPC; never deleted except with the trainer |
| `Cooldown` | Key (PK), Until | `boss` (global cooldown and failed-lobby retry share it); `npc:<trainer>:<npc>` per trainer and NPC |
| `PublishQueueItem` | Path (PK), Revision, QueuedAt, NextAttemptAt, Attempts, LastError | Website outbox: only the path — the file is rendered at upload time, so queuing a path twice is one upload. `Revision` keeps a row that was queued again during its upload |

Species, type chart and NPC teams stay in `data/*.json`, referenced by id (NPC
teams by slug). Runtime balancing values (spec §13) are planned in the DB and
editable in the UI; until then `BattleSettings` / `BossSettings` hold the spec
defaults in code.

## Flows

- **Boss** (spec §4, built): `!boss` opens a lobby (in memory, lost on restart)
  → countdown → `QueuedWork` closes it on the command queue → participant check
  → one seed per encounter draws tier, boss species, each participant's
  specimen and all hits → `BossBattle` → battle, catches, achievements and
  cooldown in one transaction → site paths queued → chat messages with the battle link.
- **PvP** (spec §6, built): `!fight @x` resolves `@x` by login or display name and
  opens a challenge in memory (expires lazily, dropped on a database switch);
  the same command from the target accepts → requirements checked again → one
  seed draws both teams of 6 (challenger first), all hits and the loot →
  `TeamBattle` → battle, loot transfer and the winner's achievements in one
  transaction → site paths queued → chat messages with the battle link.
- **NPC** (spec §7, built): `!challenge <NPC>` (name or id) → unlock check
  against `AchievementUnlock`, cooldown check (skipped in the dry run), team
  size → one seed draws the trainer's team of 6, all hits and the reward
  specimen → `TeamBattle` against the fixed team × strength → battle, tokens,
  on a first win badge and reward specimen, achievements and cooldown in one
  transaction → site paths queued → chat messages with the battle link.
- **Publishing** (spec §9, built): game logic queues site paths (`SiteOutbox`)
  in its own transaction; registration and renames queue the trainer and the
  leaderboard. `SitePublishWorker` polls the **live** outbox every 2 s,
  renders each file from the current data (`SiteGenerator`) and uploads it, or
  deletes it when its source is gone; failures back off up to 15 min and never
  give up. The dry run writes to the sandbox outbox, which nobody reads.
  "Republish everything" queues every path.
- **OBS overlay** (built): `OverlayServer` in Desktop, Kestrel on the loopback
  interface; `/overlay` is a static page that polls `/overlay/state`
  (`SiteGenerator.OverlayAsync` on the current database, so the dry run shows
  in OBS too).
- **Website** (`PokeArena.Web`, built): Blazor WASM on GitHub Pages, reads the
  JSON via `SiteData` from the data host; DTOs from `PokeArena.Contracts`
  (ADR-0009). Leaderboard and battle index poll every 15 s; a battle page
  retries a 404 every 3 s for 2 min (links can be faster than the upload).
  Setup: [website-setup.md](website-setup.md).

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
- Built (WP-06), area "Bot": "Website" card (R2 key entry, upload status with
  a plain-German reason, „Alles neu veröffentlichen“) and "OBS-Overlay" card
  with the browser source address.
- Planned: manual corrections and deletion of trainers; balancing config
  editor (all values from spec §13, live); operations (backup now).

## Keyboard shortcuts

None defined yet.
