# Pokédex on the trainer page — viewer idea (2026-11-23, branch claude/pokedex-trainer-page-improve-a4a504)

- Idea (2026-11-22, a viewer's mockup): the trainer page's Pokédex as a list of
  all 151 species with a detail box beside it, a switch to hide the unknown
  ones, species seen but not caught greyed out, and a hidden search behind a
  click on „Name“. Evaluated first, then planned in roadmap.md.
- Built:
  - Contracts: `SpeciesList` / `SpeciesData` (`arten.json`, `SitePaths.Species`)
    and the optional `TrainerPage.Seen` (ids, omitted when null — no new data
    format version, ADR-0009).
  - Core: table `SeenSpecies (TrainerId, SpeciesId)`, FK with cascade, plus an
    explicit delete in `TrainerService.DeleteAsync` like the other tables.
    `Sightings.RecordAsync` adds every species of a fight's log for every
    participant (both sides, sent in or not — the fight page lists them all);
    boss, PvP and NPC fights call it in their transaction. The migration fills
    the table once from the stored logs (`INSERT OR IGNORE … json_each(LogJson,
    '$.fighters')`). `SiteGenerator` renders `arten.json` from `SpeciesCatalog`
    (cached a day, `SpeciesCache`) and the trainer file's `Seen` (seen minus
    owned, ascending); `SiteOutbox.EnqueueEverythingAsync` queues `arten.json`,
    so „Alles neu veröffentlichen“ and the republish after an update upload it.
  - Web: `Shared/Pokedex.razor` (list, box, switch, search) instead of the old
    `<ul class="pokedex">`; `Trainer.razor` loads `arten.json` beside the
    trainer file and treats a missing or unreadable one as none (no base values,
    no „gesehen“ rows — never a page error). site.css `.dex-*`: list 470 px
    high and scrolling, box beside it stretched to the same height; below
    640 px the box hides and the same content opens below the selected row
    (`.dex-inline`). Sample data: `beispieldaten/arten.json` generated from
    `data/pokemon.json` (base data, no player data), Anna sees 11 species.
  - Tests: Core — seen rows per fight kind (boss, PvP, NPC), none after a
    rolled-back fight, deleted with the trainer, no duplicates, trainer file
    `Seen`, `arten.json` content and cache, republish lists, the migration's
    backfill on a real log. Web (bUnit) — 151 rows of three kinds, first catch
    preselected with base values, seen box without values, switch, search by
    name / number / not on unknown names, Escape, missing and broken
    `arten.json`.
  - Checked in the preview (sample data): light and dark, desktop and 375 px,
    search, switch, seen selection; contrast of the grey rows ≥ 6 : 1 in both
    themes; list and box both 470 px.
- User decisions (2026-11-23): base values from one central file instead of in
  every trainer file („Wäre zentral da nicht die einfachere Lösung?“); the
  search goes into „Neuigkeiten“ („Die Suche kannst du ruhig mit in die
  Neuigkeiten packen“); plan approved („Ja, umsetzen“) — the rest of the plan
  were Claude's proposals the user accepted with it.
- Addenda: the news section was renamed from 1.9.1 to 1.10.0 (proposal) — a
  new feature is a minor version; WP-09 and WP-12 move to 1.11.0 / 1.12.0
  (proposal, to decide at the release).
- Pitfalls:
  - The search input is `type="text"`: `type="search"` draws the browser's own
    „×“ next to ours.
  - The „Starter“ chip has the selected row's colour (`--chip-bg`); the
    selected row gives it `--surface`.
  - Not done: starting the desktop app once as a dry run — the command to stop
    it afterwards would have stopped every `PokeArena*` process on the machine,
    so the real database's first start with the migration is still to watch.
- Code review (2026-11-23): „Alle“ — all eight findings, all L.
  - L: the row loop's bound came from the files' ids — an id of 2³¹ − 1 in
    `arten.json` would never end it. Fixed: rows are always 1–151, ids outside
    are left out (bUnit theory).
  - L: readable `arten.json` with null fields (`"species":null`, an entry
    without name or types) broke the page. Fixed: such entries are left out,
    in the trainer file's Pokédex too (bUnit theory).
  - L: the box kept a species the filter or search had hidden. Fixed: the box
    shows only a visible row, otherwise „Klick auf ein Pokémon in der Liste.“
    (bUnit).
  - L: accessibility — fixed: „Name“ announces „Name – Suche öffnen“, closing
    the search returns the focus to it, „????????“ reads as „unbekannt“, the
    box is an `aria-live` region, the placeholder takes `--muted` (browser grey
    ≈ 2.2 : 1).
  - L: the PvP rollback test did not check seen species — fixed.
  - L: the privacy page's list of stored data lacked the seen species — added.
  - L: „Neuigkeiten“ said „rechts“, the box is below the row on a phone —
    reworded.
  - L: version references in roadmap.md and CLAUDE.md §3 — fixed.
  - Second pass, „Alle“:
    - M: the hidden „unbekannt“ texts (`.visually-hidden`, absolute) escaped
      the scrolling list and stretched the page to 6 919 px instead of 1 344 px
      (measured in the preview). Fixed: `.dex-rows` is `position: relative`.
    - L: a null inside `types` still broke the page — such entries are left
      out too (bUnit case).
    - L: the focus test only counted calls — it now checks the target is the
      „Name“ button.
- Deliberately not done (→ backlog): a „Gesehen“ count in the stats row;
  rarity in the box; remembering the switch.
