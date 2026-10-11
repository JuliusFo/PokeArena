# PokeArena

Working language with the user: **German**. Code, comments, commits, docs: **English**.
Status: 2026-10-11 · no release yet (pre-v0.1.0, specification phase)

## 1. Where to find what

| File | Content | When to read |
|---|---|---|
| `docs/spec.md` | Game rules, commands, config defaults (source of truth for gameplay) | Before any game-logic or chat-text work |
| `docs/architecture.md` | Data model, game flows, publishing outbox, desktop UI concept | Before touching Core or Desktop structure |
| `docs/decisions/` | ADRs (stack, Twitch, site data, determinism) | Before changing stack or architecture |
| `docs/roadmap.md` | Planned work packages WP-NN, user decisions, open questions | At the start of every package |
| `docs/backlog.md` | Unplanned ideas, grouped by topic | When an idea comes up — park it there |
| `docs/work-packages.md` | Package overview + log per finished package | After finishing a package |
| `CHANGELOG.md` | Release notes (Keep a Changelog, SemVer) | Before tagging a release |
| `src/<Project>/CLAUDE.md` | Pitfalls of that project (loads automatically; created in WP-00) | — |

## 2. Goal and audience

A persistent Pokemon collecting and battling game played via Twitch chat on a
small channel. A WPF app on the streamer's PC runs the bot; a static website
shows leaderboard, trainer pages and battle logs.

- Developer (the user): experienced .NET developer → explain briefly and technically.
- Streamer (operates the desktop app, not a developer) → UI texts and setup
  steps must work without technical knowledge.
- Players: casual Twitch viewers → all chat and website text in simple German.

## 3. Versions and invariants

| Version | Content | Packages |
|---|---|---|
| 0.1.0 | Scaffold, CI, bot answers a test command in chat | WP-00, WP-01 |
| 0.2.0 | Registration, persistent data | WP-02 |
| 0.3.0 | Battle engine, `!boss` playable | WP-03 |
| 0.4.0 | PvP `!fight` | WP-04 |
| 0.5.0 | NPC fights `!challenge` | WP-05 |
| 0.6.0 | Public website + OBS overlay | WP-06 |
| 1.0.0 | Stream-ready: mod commands, deletion, backup | WP-07 |

**Invariants — never weaken:**

- A trainer is identified by Twitch user id, never by display name. (spec §1)
- SQLite is the single source of truth; every website file is derived and
  regenerable. (ADR-0001, ADR-0003)
- No player data ever enters the git repository. (ADR-0003)
- Secrets live only in the Windows credential store — never in DB, config or
  repo. (ADR-0002)
- `PokeArena.Core` has no UI dependency. (ADR-0001)
- Randomness and time only via injected interfaces; every battle stores seed
  and full log. (ADR-0004)
- Species base data is referenced by id; its values are never copied into the
  DB. (ADR-0001)
- Achievements are never revoked; the starter specimen is never transferred.
  (spec §1, §6)

## 4. Stack

Changes only via ADR.

| Area | Decision | Notes |
|---|---|---|
| Runtime | .NET 10 (`net10.0`, `net10.0-windows`), `.slnx` | Version via `Directory.Build.props` |
| Desktop | WPF + WPF-UI + CommunityToolkit.Mvvm, Microsoft.Extensions.Hosting | Same stack as the user's DeskCycle project |
| Storage | EF Core + SQLite | Migrations in Core |
| Twitch | EventSub WebSocket (read), Helix Send Chat Message (write), Device Code Flow | TwitchLib behind own interfaces (ADR-0002) |
| Website | Blazor WebAssembly on GitHub Pages | App only, no data (ADR-0003) |
| Site data | Cloudflare R2 via `r2.dev`, `AWSSDK.S3` | Base URL is config (ADR-0003) |
| Tests | **open** — proposal: xUnit v3 | Decide in WP-00 |

**Do not use:** IRC chat · GitHub (Contents API, Pages, commits) for player
data · `NuGet.config` (all packages from nuget.org) · `Random`, `DateTime.Now`
or `DateTimeOffset.Now` directly in Core · UI automation tests (test ViewModels
directly).

## 5. Solution layout

```
PokeArena.slnx
├─ src/
│  ├─ PokeArena.Core/      # domain, engine, Twitch, data, publishing — no UI
│  ├─ PokeArena.Desktop/   # WPF host, ViewModels, local overlay endpoint
│  └─ PokeArena.Web/       # Blazor WASM, reads JSON from R2
├─ tests/PokeArena.Core.Tests/
└─ data/                   # pokemon.json, typen.json, npcs.json (versioned base data)
```

- Desktop → Core. Tests → Core. Core → no project.
- Web references neither Core nor Desktop (no EF Core or Twitch in WASM).
  How the site JSON DTOs are shared is **open** (WP-06).

## 6. Core terms (details: docs/architecture.md)

- **Trainer** — registered viewer, keyed by Twitch user id.
- **Species (Art)** — one of 151 Gen-1 Pokemon from `data/pokemon.json`, exactly one type.
- **Specimen (Exemplar)** — one owned instance; duplicates allowed; one is the protected starter.
- **Achievement (AV)** — threshold of distinct species owned; unlocks an NPC; permanent.
- **Boss encounter** — lobby + instantly resolved group fight; survivors catch the boss.
- **Challenge** — open PvP request (one per trainer); the winner takes a specimen.
- **NPC** — fixed team, unlocked by achievement, per-trainer cooldowns.
- **Publish queue** — outbox from game logic to the R2 upload worker.

## 7. Way of working

- Work in packages: branch `wp/NN-short-name` (smaller topics `feat/…`,
  `fix/…`, `docs/…`), always from `main`. State the plan before starting, then
  implement; finish a package before starting another. On UX uncertainty build
  the simplest variant and note the question in roadmap.md instead of blocking.
- Done means: build with warnings as errors and tests green, ADR if needed, log
  in work-packages.md, CHANGELOG entry, then a short summary (built / open / to
  test).
- Small, topical commits, Conventional Commits (`feat:`, `fix:`, `test:`,
  `docs:`, `refactor:`).
- Core logic first with tests, then UI. Unit tests without external
  dependencies; integration tests (Twitch, R2) separate and skip cleanly when
  the infrastructure is missing.
- Don't pull features forward from later packages. Ideas go to backlog.md,
  never implemented on the side.

## 8. User context

- Solo project. The user builds it **for another streamer**; the app runs on
  the streamer's Windows PC together with OBS and the overlay.
- The user sets up the dedicated bot account and its Twitch tokens. The
  broadcaster still has to grant `channel:bot` or make the bot a moderator.
- Small channel: load assumption ~30 concurrent viewers, a few hundred trainers.
- Dev machine: Windows 11, PowerShell (Git Bash available); **no Python
  installed** — write scripts in PowerShell or as `dotnet` tools.
- Base data `data/pokemon.json` is still to be supplied (spec §14).

## 9. Next

Settle boss balancing (damage formula, boss scaling, simulator) for WP-03;
start WP-00 scaffold. Details: docs/roadmap.md.
