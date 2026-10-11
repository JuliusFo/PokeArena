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
- Backups are a single DB file (see roadmap WP-07 for the WAL caveat).
