# Work packages

Overview and a log entry per finished package. Plans and open questions live
in [roadmap.md](roadmap.md).

| WP | Name | Version | Status |
|---|---|---|---|
| WP-00 | Scaffold | 0.1.0 | done 2026-10-11 |
| WP-01 | Twitch connection | 0.1.0 | planned |
| WP-02 | Domain foundation | 0.2.0 | planned |
| WP-03 | Battle engine + boss | 0.3.0 | planned |
| WP-04 | PvP | 0.4.0 | planned |
| WP-05 | NPC fights | 0.5.0 | planned |
| WP-06 | Website | 0.6.0 | planned |
| WP-07 | Operations | 1.0.0 | planned |

## Log

### WP-00 — Scaffold (2026-10-11, branch wp/00-scaffold)
- Built: `PokeArena.slnx` with Core, UI (Razor class library), Desktop (WPF host
  with `BlazorWebView`), Core.Tests and Integration.Tests; shared build
  settings (warnings as errors, central package versions, `global.json`,
  `nuget.config`, `.gitattributes`, `.editorconfig`); Generic Host, Serilog
  file logs, global exception handlers, single-instance guard, window chrome
  following the Windows theme; `AppPaths`/`PokeArenaOptions`
  (`--PokeArena:DataDirectory`); `ISecretStore` + Credential Manager
  implementation; banned-API analyzer for clock/randomness in Core; CI on
  `windows-latest`. App starts with an empty welcome shell; 6 unit and
  3 integration tests green locally.
- User decisions: xUnit v3; Meziantou Credential Manager package; Blazor
  Hybrid instead of WPF-UI (ADR-0007); `nuget.config` limited to nuget.org
  instead of "no NuGet.config" (ADR-0001 addendum).
- Addenda: ADR-0001 and ADR-0004 (analyzer enforcement; seeded `Random`
  allowed).
- Pitfalls: a global DevExpress feed on the dev machine broke the build with
  NU1507 under central package management → `nuget.config` with `<clear />`.
  `core.autocrlf=true` on the dev machine still prints "LF will be replaced by
  CRLF" on `git add` — harmless, the repository stores LF.
- Deliberately not done: Web project (WP-06), bUnit/UI test project (with the
  first components that have logic, WP-01), release workflow (WP-07), app icon.

Template per entry:

```
### WP-NN — Name (YYYY-MM-DD, branch wp/NN-short-name)
- Built:
- User decisions:
- Addenda:
- Pitfalls:
- Deliberately not done (→ backlog):
```
