# PokeArena

Working language with the user: **German**. Code, comments, commits, docs: **English**.
Status: 2026-11-10 · v1.6.1 released (white text in the „Schaden“ bars on darkened chat colours) · next the „Statistik“ page (→ 1.7.0), then WP-09 (→ 1.8.0) after a few streams

## 1. Where to find what

| File | Content | When to read |
|---|---|---|
| `docs/spec.md` | Game rules, commands, config defaults (source of truth for gameplay) | Before any game-logic or chat-text work |
| `docs/architecture.md` | Data model, game flows, publishing outbox, desktop UI concept | Before touching Core or Desktop structure |
| `docs/decisions/` | ADRs (stack, Twitch, site data, determinism, base data/types, balancing, desktop UI, no official assets, site contracts, self-update) | Before changing stack or architecture |
| `docs/roadmap.md` | Planned work packages WP-NN, user decisions, open questions | At the start of every package |
| `docs/website-setup.md` | One-time setup of Cloudflare (R2, cache, rate limit, CORS), GitHub Pages and the desktop app | When bringing the website online or moving it |
| `docs/backlog.md` | Unplanned ideas, grouped by topic | When an idea comes up — park it there |
| `docs/chat-texts.md` | Every chat text of the bot | When a chat text changes (same change) |
| `docs/review-X.Y.Z.md` | Whole-solution reviews with findings and user decisions | Before the next whole review |
| `docs/work-packages.md` | Package overview + log per finished package | After finishing a package |
| `CHANGELOG.md` | Release notes (Keep a Changelog, SemVer) | Before tagging a release |
| `src/<Project>/CLAUDE.md` | Pitfalls of that project (Core, UI, Desktop, Web; loads automatically) | — |
| `.claude/skills/` | `/ship-wp` (one package from plan to green CI), `/branch-code-review` (one branch, separate reviewer agent, `checklist.md`), `/complete-code-review` (whole solution, six agents → `docs/review-X.Y.Z.md`), `/release` | Before a package, review or release; keep the checklist in line with §3 |

## 2. Goal and audience

A persistent Pokemon collecting and battling game played via Twitch chat on a
small channel. A WPF app on a Windows machine (§8) runs the bot; a static website
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
| 0.7.0 | Commands only while the stream is live | WP-07 |
| 1.0.0 | Stream-ready: mod commands, deletion, backup | WP-08 |
| 1.1.0 | Review 1.0.3 fixes, boss lobby (2 min, late-join extension, HP left) | R1, feat/boss-lobby |
| 1.2.0 | Fight page shows how each Pokémon ended | feat/battle-end-state |
| 1.3.0 | Website page „Neuigkeiten“ (player-visible changes per version) | WP-10 |
| 1.4.0 | Boss pick uniform over distinct species, fight log in chat colours, log numbering and HP bar fixes | feat/boss-pick-per-species, feat/fight-log-colours, fix/battle-log-numbering, feat/hp-bar-alignment |
| 1.4.1 | Fight log colours only the boss's name, critical hits on gold, fighter rows keep the HP bar on the right | feat/fight-log-boss-name-crit, fix/fighter-row-wrap |
| 1.4.2 | Fight cards as wide as the page again (1.4.1 widening reverted) | fix/fight-card-width |
| 1.5.0 | Mods shorten the boss cooldown for up to 24 h (`!bosscooldown`); desktop app updates itself from the GitHub releases | feat/boss-cooldown-reduced, WP-11 |
| 1.6.0 | Fight page „Schaden“ box (damage per side and ranking in chat colours), crisp HP numbers | feat/damage-meter, fix/hp-text-crisp |
| 1.6.1 | „Schaden“ bars in the darkened name colour with white text, names in that colour in both themes | feat/damage-text-white |
| 1.7.0 | Website page „Statistik“: boss participations, PvP fights, bad-luck streaks, rarities, rivals, boss record | feat/statistics-page |
| 1.8.0 | Balancing with real stream data | WP-09 |
| 2.0.0 | Parked: website and overlay served from the bot's server (roadmap.md) | — |

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
| Site data | Cloudflare R2 behind a custom domain with cache (no `r2.dev`), `AWSSDK.S3` | Base URL is config (ADR-0003 + addendum) |
| Secrets | Windows Credential Manager via `Meziantou.Framework.Win32.CredentialManager` | Behind `ISecretStore` (ADR-0002) |
| Updates | Velopack (`Setup.exe`, `Portable.zip`, delta packages on the GitHub release; `vpk` local tool, same version) | Behind `IAppUpdater`, installs only while the bot is idle (ADR-0010) |
| Tests | xUnit v3 on Microsoft Testing Platform; bUnit for components | ADR-0001 addendum |

**Do not use:** IRC chat · GitHub (Contents API, Pages, commits) for player
data · package feeds other than nuget.org · WPF-UI, CommunityToolkit.Mvvm
(ADR-0007) · in Core: clock and unseeded randomness (`DateTime.Now`,
`new Random()` … — the build fails, ADR-0004) · UI automation tests.

## 5. Solution layout

```
PokeArena.slnx
├─ src/
│  ├─ PokeArena.Contracts/ # site JSON records shared by Core and Web (ADR-0009)
│  ├─ PokeArena.Core/      # domain, engine, Twitch, data, publishing — no UI, no Windows
│  ├─ PokeArena.UI/        # Razor components + CSS of the desktop app
│  ├─ PokeArena.Desktop/   # WPF host: BlazorWebView, logging, Windows services, OBS overlay endpoint
│  └─ PokeArena.Web/       # public site, Blazor WASM on GitHub Pages, reads JSON from R2
├─ tests/
│  ├─ PokeArena.Core.Tests/        # unit tests, nothing outside the process
│  ├─ PokeArena.UI.Tests/          # bUnit component tests (desktop UI)
│  ├─ PokeArena.Web.Tests/         # bUnit component tests (website)
│  └─ PokeArena.Integration.Tests/ # real infrastructure (Credential Manager, Twitch, R2, overlay port)
├─ tools/                  # dev scripts, e.g. GeneratePokemonData.cs (`dotnet run <file>`)
└─ data/                   # pokemon.json, typen.json, npcs.json (versioned base data)
```

- `data/pokemon.json` is generated — change `tools/GeneratePokemonData.cs`
  and rerun it instead of editing the JSON (ADR-0005).

- Desktop → UI, Core. UI → Core. Core → Contracts. Web → Contracts.
  Contracts → no project. Core.Tests → Core; UI.Tests → UI; Web.Tests → Web;
  Integration.Tests → Desktop.
- Web references neither Core nor Desktop (no EF Core or Twitch in WASM,
  ADR-0009).

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
  `fix/…`, `docs/…`; review fixes as package R<n> on `wp/r<n>-review-X.Y.Z`),
  always from `main`. State the plan before starting, then
  implement; finish a package before starting another (`/ship-wp` runs a
  package in this order). If it is unclear *which* element a request means, ask
  once before building; if only *how* it should look or behave is unclear,
  build the simplest variant and note the question in roadmap.md instead of
  blocking.
- **Decided is only what the user decided.** Rules, bans and choices Claude
  proposes itself go into docs, ADRs and `CLAUDE.md` files marked "Proposal
  (not confirmed)" until the user agrees — otherwise the next session takes
  them as settled.
- Done means: build with warnings as errors and tests green, ADR if needed, log
  in work-packages.md, CHANGELOG entry, a line on the website's „Neuigkeiten“
  page if players notice the change (user decision 2026-11-09, review WP-10),
  then a short summary (built / open / to test).
- Small, topical commits, Conventional Commits (`feat:`, `fix:`, `test:`,
  `docs:`, `refactor:`).
- Integration (user decision 2026-10-13): code goes through a PR — `main`
  requires the CI check "Build and test (Windows)", which builds from a clean
  checkout. Once it is green, merge locally (`git merge --ff-only <branch>` on
  an up-to-date `main`, then push): the commit ids stay the same, GitHub marks
  the PR as merged, and the release tag can be set right away. No GitHub
  rebase/auto-merge — it rewrites commit ids. Small docs-only fixes may go
  straight to `main`.
- Release: after the live test, add the `[X.Y.Z] - date` section to the
  CHANGELOG and set `<Version>` in `Directory.Build.props` on the package
  branch; after the merge tag `vX.Y.Z` on `main` and push the tag.
  The tag also deploys the website (`pages.yml`, user decision 2026-11-10):
  `main` collects website changes and „Neuigkeiten“ lines unpublished until
  the release, so merging a branch never puts them live early.
- Core logic first with tests, then UI. Unit tests without external
  dependencies; integration tests (Twitch, R2) separate and skip cleanly when
  the infrastructure is missing.
- Don't pull features forward from later packages. Ideas go to backlog.md,
  never implemented on the side.

## 8. User context

- Solo project. The user builds it **for another streamer**. Planned was the
  streamer's Windows PC together with OBS and the overlay; in fact the app runs
  on the user's Windows server (apparently for good, 2026-11-10) and the
  overlay is not used. Docs still assuming the streamer's PC: roadmap.md
  „Version 2“.
- The user sets up the dedicated bot account and its Twitch tokens. Neither
  `channel:bot` nor moderator status is required (ADR-0002 addendum); the
  streamer makes the bot a moderator anyway (slow mode).
- Small channel: load assumption ~30 concurrent viewers, a few hundred trainers.
- Dev machine: Windows 11, PowerShell (Git Bash available); **no Python
  installed** — write scripts in PowerShell or as `dotnet` tools.
- Base data uses official Gen-1 values (ADR-0005).

## 9. Next

1.6.1 (`feat/damage-text-white`): the „Schaden“ bars take the darkened name colour with white text like the HP
numbers, and names keep that colour in the dark theme too — a test with the streamer whether names stay readable on
the dark page; if not, the lighter dark-theme variant of 1.6.0 comes back for names.
1.6.0 is released: a „Schaden“ box on the fight page (`feat/damage-meter`: side totals and a ranking in the
trainers' chat colours, damage that arrived from the stored log) and crisp HP numbers (`fix/hp-text-crisp`);
after updating the streamer clicks „Alles neu veröffentlichen“ once so older fights get the box.
1.5.0 (installation: README, „Installing and running“):
`!bosscooldown` (`feat/boss-cooldown-reduced`: mods shorten the boss cooldown
for up to 24 hours, half by default), WP-11 automatic update (ADR-0010:
Velopack, installs 10 min after the stream) and HP numbers inside the fight
page's HP bar. 1.5.0 is the one-time switch: install it with `Setup.exe` as the
same Windows user on the server; from 1.5.1 on releases arrive by themselves.
1.4.2 made the fight cards as wide as the page again; 1.4.1 coloured only the
boss's name in the fight log, put critical hits on gold and kept long names
from pushing a fighter's HP bar onto its own line — after updating to 1.4.1 or
later the streamer clicks „Alles neu veröffentlichen“ once. 1.4.0 made the
boss pick uniform over distinct species and showed trainers in their Twitch chat
colour in the fight log; 1.3.0 brought the
„Neuigkeiten“ page (WP-10), 1.2.0 the fight page's end state, 1.1.0 R1 (fixes
of the whole review 1.0.3, docs/review-1.0.3.md) and the boss lobby. Next:
the „Statistik“ page (1.7.0, roadmap.md), then after a few streams WP-09 balancing (1.8.0)
(`N = 4` dip, PvP loot preferring missing species, NPC difficulty; no runtime
balancing editor). Parked for version 2: website and overlay served from the
bot's server through a Cloudflare Tunnel instead of GitHub Pages + R2. Ideas
meanwhile go to docs/backlog.md. Details: docs/roadmap.md.
