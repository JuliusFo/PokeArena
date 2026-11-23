# Pokédex switch remembered — user request (2026-11-23, branch claude/pokedex-toggle-persistence-c87bd9)

- Request: keep „Alle / Nur gefangene“ in the Pokédex (backlog item from the
  first Pokédex version).
- Built: `ViewerSettings.PokedexViewKey` (`pokearena.pokedex`, „gefangen“ /
  „alle“) with `OnlyCaughtAsync` / `SavePokedexViewAsync`, both catching
  `JSException` like the log view. `Shared/Pokedex.razor` reads it in
  `OnInitializedAsync` and saves on each click; one pick for every trainer
  page. `WebTestContext` plans the new `getItem` (null = „Alle“). bUnit tests:
  clicks save „gefangen“ / „alle“, a stored „gefangen“ opens with the caught
  ones only, blocked storage starts with „Alle“ and the switch still works.
  Privacy page, „Neuigkeiten“ (under 1.10.1, proposal), CHANGELOG, spec §9,
  roadmap, backlog item removed, Web `CLAUDE.md`.
- Deliberately not done: the switch is not on „Einstellungen“ — it is picked
  on the trainer page only (not asked for).
