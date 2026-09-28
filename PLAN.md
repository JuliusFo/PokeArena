# PokeArena — Implementation Plan

Companion to [SPEC.md](SPEC.md). Estimates assume solo development by an
experienced .NET developer; "day" = one focused person-day. Total for v1:
**≈ 25–32 days**. Every milestone ends in a state that could run on stream.

## Solution layout

```
PokeArena.slnx
├─ src/
│  ├─ PokeArena.Core/          # net10.0 — no UI dependencies
│  │  ├─ Twitch/               # EventSub client, Helix sender, device-flow auth, rate limiter
│  │  ├─ Commands/             # parsing, routing, cooldown/permission checks, dialog states
│  │  ├─ Game/                 # battle engine, boss encounter, PvP, NPC, achievements
│  │  ├─ Data/                 # EF Core model, SQLite, migrations, base-data loader
│  │  └─ Publishing/           # ISitePublisher, GitHub Contents API impl., outbox worker
│  ├─ PokeArena.Desktop/       # net10.0-windows, WPF (WPF-UI + CommunityToolkit.Mvvm,
│  │                           #   Microsoft.Extensions.Hosting — same stack as DeskCycle)
│  └─ PokeArena.Web/           # Blazor WebAssembly, static, deployed to GitHub Pages
├─ tests/
│  └─ PokeArena.Core.Tests/
└─ data/
   ├─ pokemon.json             # base data (OPEN — supplied by owner)
   ├─ typen.json               # Gen-1 type chart, house rules applied
   └─ npcs.json                # 17 NPC teams + strength multipliers
```

Key packages: `TwitchLib.Api` (Helix), `TwitchLib.EventSub.Websockets`
(preview — wrapped behind own interfaces so a hand-rolled WebSocket client can
replace it if it misbehaves), `Microsoft.EntityFrameworkCore.Sqlite`,
`CommunityToolkit.Mvvm`, `WPF-UI`.

## Data model (sketch)

- `Trainer` (TwitchUserId PK, DisplayName, RegisteredAt)
- `Specimen` (Id, TrainerId, SpeciesId, CaughtAt, Origin, IsStarter)
- `Battle` (Id, Kind, StartedAt, Seed, WinnerRef, LogJson, TransferredSpecimenId?)
- `BattleParticipant` (BattleId, TrainerId/NpcId, TeamJson)
- `AchievementUnlock` (TrainerId, Threshold, UnlockedAt)
- `Cooldown` (Key, Until) — boss global, per trainer+NPC, failed-lobby retry
- `OpenChallenge` (ChallengerId, TargetId, ExpiresAt)
- `PublishQueueItem` (Id, Path, Payload, State, Attempts)

Species base data stays in JSON (versioned in git), referenced by id — the DB
never copies balancing values.

## Milestones

| # | Scope | Demo criterion | Est. |
|---|-------|----------------|------|
| M0 | Solution scaffold, DI/hosting, config system, credential store, CI (build + tests) | `dotnet test` green in CI | 1 |
| M1 | Twitch: device-flow auth, EventSub read, Helix send, rate limiter; WPF shell with connect button, status, live chat log | Bot answers a test command in the channel | 3–4 |
| M2 | Domain foundation: EF Core model + migrations, base-data loader, `!registrieren`, trainer list in UI | Viewers can register; data survives restart | 2–3 |
| M3 | Battle engine (damage, types, crits, seed, log) + boss encounter incl. lobby, rarity draw, cooldowns, achievements | `!boss` playable end-to-end | 4–5 |
| M4 | PvP: challenge/accept dialog, timeout, random teams, loot transfer | `!fight` playable | 3–4 |
| M5 | NPC fights + unlock announcements (reward slot stubbed until E22 is decided) | `!challenge Eich` playable | 2–3 |
| M6 | Website: Blazor pages (leaderboard, battle, trainer, overlay), GitHub Actions deploy, publisher + outbox, local overlay endpoint | Battle link in chat opens a live page | 4–6 |
| M7 | Operations: mod commands, deletion flow, backup, republish-all, README, message wording pass | Stream-ready | 2–3 |

Milestone order is deliberate: M3 needs `pokemon.json`; if the base data is
still missing, M6 can be pulled forward — everything in it works with dummy
data.

## Testing strategy

- Engine, formulas, rarity draw, cooldowns, command parsing and the outbox
  are pure Core code with injected RNG/clock → deterministic unit tests, no
  network. Seed-based golden tests for whole battles.
- Twitch and GitHub sit behind interfaces (`IChatTransport`, `IChatSender`,
  `ISitePublisher`) with fake implementations for tests and a "dry run" mode
  in the UI.
- No simulated-mouse UI testing; ViewModels are tested directly.

## Risks

| Risk | Mitigation |
|------|-----------|
| `TwitchLib.EventSub.Websockets` is preview | wrapped behind own interface; hand-rolled client is a ~1-day fallback |
| EventSub chat is officially beta (breaking changes possible) | transport isolated in one module; IRC fallback would slot behind the same interface |
| Pages deploy latency (~1 min) annoys during streams | battle page retry state (SPEC §9); switch to Cloudflare R2 behind `ISitePublisher` is a ~1-day change |
| Base data / E22 pending | milestones reordered so nothing idles |
| Balancing needs live tuning | all values in UI-editable config; battles store seeds for post-hoc analysis |

## Working agreements

- Code, comments, docs: English. Chat output, UI, website: German.
- Scope and approach are confirmed before implementation of each milestone.
- No NuGet.config in the repo; all packages from nuget.org.
- Commits per milestone step; version stamped via Directory.Build.props like
  in DigitalDeskCycle.
