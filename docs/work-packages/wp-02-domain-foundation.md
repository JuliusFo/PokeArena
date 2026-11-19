# WP-02 — Domain foundation (2026-10-13, branch wp/02-domain-foundation)

- Built: EF Core + SQLite model `Trainer` / `Specimen` (UTC-tick timestamps,
  enums as text, filtered unique index "one starter per trainer"), initial
  migration, `dotnet-ef` as local tool; live database
  `%LOCALAPPDATA%\PokeArena\pokearena.db` in WAL mode, migrated at startup;
  `SpeciesCatalog` loads and validates `data/pokemon.json` (shipped with the
  app); `GameDatabase` switches between live database and an in-memory dry-run
  sandbox; `TrainerService` (registration in one transaction, display-name
  refresh with cache, trainer list); `!registrieren`; `CommandAccess.Registered`
  with a hint for unregistered users; the processor drops real messages during
  the dry run and simulated ones outside it. UI: Bot/Trainer tabs, read-only
  trainer list with search, "Probelauf aktiv" badge. Tests: 80 unit (in-memory
  SQLite, real base data), 12 bUnit.
- Live test 2026-10-13: `!registrieren` answered in `ShiyoFurball`, the
  registration survived a restart, the dry run kept its test trainers apart.
- User decisions: the dry run gets its own sandbox database that starts empty
  each time; real messages are not processed meanwhile.
- Addenda: none (EF Core + SQLite already in ADR-0001). `DryRunMode` moved to
  the Core root namespace; Microsoft packages aligned to 10.0.13 (EF Core 10.0.13
  pulled a newer `Logging.Abstractions`, NU1605 under warnings as errors).
- Pitfalls: see `src/PokeArena.Core/CLAUDE.md` (contexts only via
  `GameDatabase.Open()`, UTC ticks, migration command, caches reset on
  `Switched`). `dotnet new tool-manifest` on SDK 10 writes `dotnet-tools.json`
  to the repo root, not `.config/`.
- Deliberately not done: backup before migrations (WP-08), editing or
  deleting trainers (WP-08), balancing values in the database (WP-03),
  throttling repeated hints (backlog).
