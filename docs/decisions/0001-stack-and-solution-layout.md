# ADR-0001: Stack and solution layout

Status: accepted · Date: 2026-09-03

## Context

The bot runs on the streamer's Windows PC next to OBS; the user develops solo
and already uses WPF-UI + CommunityToolkit.Mvvm + Generic Host in the
DeskCycle project. Game state must survive restarts, and the public website
has to be regenerable at any time. (Concept frozen 2026-09-03.)

## Decision

- .NET 10, solution file `PokeArena.slnx`, version stamped via
  `Directory.Build.props`.
- `PokeArena.Core` (`net10.0`) holds all logic and has **no UI dependency**.
  `PokeArena.Desktop` (`net10.0-windows`, WPF) is the host.
  `PokeArena.Web` (Blazor WebAssembly) references neither.
- **EF Core + SQLite is the single source of truth** for trainers, specimens,
  battles, achievements, cooldowns, challenges, publish queue and balancing
  config. Everything published is derived from it.
- Species, type chart and NPC teams stay in versioned JSON under `data/` and
  are referenced by id; the DB never copies their values.
- All packages from nuget.org; no `NuGet.config` in the repo.

Deliberately **not**: a server-side database, a hosted bot, UI automation
tests.

## Consequences

- Balancing changes to base data are a git diff, not a migration.
- Core is fully unit-testable; ViewModels are tested directly.
- Backups are a single DB file (see roadmap WP-08 for the WAL caveat).

## Addendum 2026-10-11 (WP-00)

- The desktop UI is Blazor Hybrid instead of WPF-UI + CommunityToolkit.Mvvm
  (ADR-0007); `PokeArena.UI` (Razor class library) joins the layout.
- Tests: xUnit v3 on Microsoft Testing Platform (user decision). Integration
  tests live in `tests/PokeArena.Integration.Tests`.
- Package versions are managed centrally (`Directory.Packages.props`).
- "No `NuGet.config`" is replaced by a `nuget.config` that clears all
  machine-wide feeds and maps every package to nuget.org (user decision): a
  global DevExpress feed on the dev machine broke the build (NU1507). The
  intent — packages only from nuget.org — is now enforced, not just agreed.
- Logging: Serilog file sink under `%LOCALAPPDATA%\PokeArena\logs`.
- Secrets: `Meziantou.Framework.Win32.CredentialManager` (user decision).

## Addendum 2026-11-08 (review 1.0.3)

- The Decision's list of database contents is narrowed: open PvP challenges
  and the boss lobby are short-lived and stay in memory (spec §10), and
  balancing values are code defaults that change by release, without a
  runtime editor (user decision 2026-11-04, roadmap WP-09). Neither is a
  "source of truth" question: nothing published depends on them.
