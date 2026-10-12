# PokeArena

Working language with the user: **German**. Code, comments, commits, docs: **English**.
Status: 2026-10-13 · v0.1.0 (WP-00, WP-01 done) · WP-02 next

## 1. Where to find what

| File | Content | When to read |
|---|---|---|
| `docs/spec.md` | Game rules, commands, config defaults (source of truth for gameplay) | Before any game-logic or chat-text work |
| `docs/architecture.md` | Data model, game flows, publishing outbox, desktop UI concept | Before touching Core or Desktop structure |
| `docs/decisions/` | ADRs (stack, Twitch, site data, determinism, base data/types, balancing, desktop UI, no official assets) | Before changing stack or architecture |
| `docs/roadmap.md` | Planned work packages WP-NN, user decisions, open questions | At the start of every package |
| `docs/backlog.md` | Unplanned ideas, grouped by topic | When an idea comes up — park it there |
| `docs/work-packages.md` | Package overview + log per finished package | After finishing a package |
| `CHANGELOG.md` | Release notes (Keep a Changelog, SemVer) | Before tagging a release |
| `src/<Project>/CLAUDE.md` | Pitfalls of that project (Core, UI, Desktop; loads automatically) | — |

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
- No official Pokémon assets (sprites, artwork, sounds, logos) in repo, app,
  website or overlay — not even fetched at runtime. (ADR-0008)

## 4. Stack

Changes only via ADR.

| Area | Decision | Notes |
|---|---|---|
| Runtime | .NET 10 (`net10.0`, `net10.0-windows10.0.19041.0`), `.slnx` | Version in `Directory.Build.props`, package versions in `Directory.Packages.props`, feeds in `nuget.config` (nuget.org only) |
| Desktop | Blazor Hybrid: thin WPF host + `BlazorWebView`, UI in Razor class library; Generic Host, Serilog | As in FerretSharp (ADR-0007) |
| Storage | EF Core + SQLite | Migrations in Core |
| Twitch | EventSub WebSocket (read), Helix Send Chat Message (write), Device Code Flow | Own lean client (`HttpClient`, `ClientWebSocket`) behind own interfaces (ADR-0002) |
| Website | Blazor WebAssembly on GitHub Pages | App only, no data (ADR-0003) |
| Site data | Cloudflare R2 via `r2.dev`, `AWSSDK.S3` | Base URL is config (ADR-0003) |
| Secrets | Windows Credential Manager via `Meziantou.Framework.Win32.CredentialManager` | Behind `ISecretStore` (ADR-0002) |
| Tests | xUnit v3 on Microsoft Testing Platform; bUnit for components | ADR-0001 addendum |

**Do not use:** IRC chat · GitHub (Contents API, Pages, commits) for player
data · package feeds other than nuget.org · WPF-UI, CommunityToolkit.Mvvm
(ADR-0007) · in Core: clock and unseeded randomness (`DateTime.Now`,
`new Random()` … — the build fails, ADR-0004) · UI automation tests.

## 5. Solution layout

```
PokeArena.slnx
├─ src/
│  ├─ PokeArena.Core/      # domain, engine, Twitch, data, publishing — no UI, no Windows
│  ├─ PokeArena.UI/        # Razor components + CSS of the desktop app
│  ├─ PokeArena.Desktop/   # WPF host: BlazorWebView, logging, Windows services; later overlay endpoint
│  └─ PokeArena.Web/       # (WP-06) Blazor WASM, reads JSON from R2
├─ tests/
│  ├─ PokeArena.Core.Tests/        # unit tests, nothing outside the process
│  ├─ PokeArena.UI.Tests/          # bUnit component tests
│  └─ PokeArena.Integration.Tests/ # real infrastructure (Credential Manager, Twitch; later R2)
├─ tools/                  # dev scripts, e.g. GeneratePokemonData.cs (`dotnet run <file>`)
└─ data/                   # pokemon.json, typen.json, npcs.json (versioned base data)
```

- `data/pokemon.json` is generated — change `tools/GeneratePokemonData.cs`
  and rerun it instead of editing the JSON (ADR-0005).

- Desktop → UI, Core. UI → Core. Core → no project. Core.Tests → Core;
  UI.Tests → UI; Integration.Tests → Desktop.
- Web references neither Core nor Desktop (no EF Core or Twitch in WASM).
  How the site JSON DTOs are shared is **open** (WP-06).

## 6. Core terms (details: docs/architecture.md)

- **Trainer** — registered viewer, keyed by Twitch user id.
- **Species (Art)** — one of 151 Gen-1 Pokemon from `data/pokemon.json`, Gen-1 stats, one or two types.
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
- Base data uses official Gen-1 values (ADR-0005).

## 9. Next

Start WP-02 (domain foundation: EF Core, base-data loader, `!registrieren`).
Still open before later packages: NPC reward (E22), operations on the streamer's PC. Details: docs/roadmap.md.
