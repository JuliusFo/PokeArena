# Pokédex box „Verlauf“ — user feedback (2026-11-27, branch claude/pokedex-details-layout-76a090)

- Feedback: the box next to the Pokédex list has much free space below the
  base values; when a Pokémon was caught and when it was last seen could go
  there.
- Evaluation: per specimen the database had `CaughtAt`, `AcquiredAt` and
  `Origin`, but the trainer file exported only species, count and starter.
  „Last seen“ was not stored (`SeenSpecies` had no time) but could be filled
  from the fight logs like the table itself. A PvP-won specimen keeps the
  original catcher's `CaughtAt`, so the trainer's own date is `AcquiredAt`.
- Built: `SeenSpecies` gets `FirstSeenAt`/`FirstBattleId` and
  `LastSeenAt`/`LastBattleId`; migration `SeenSpeciesDates` fills them from the
  logs (window functions over `json_each`, by time then id). `Sightings.RecordAsync`
  takes the saved battle and updates the latest fight (and the first of a new
  row); boss, PvP and NPC fights call it after their first save. The trainer
  file gets `PokedexEntry.Specimens` (`SpecimenInfo`: source Starter / Boss /
  Npc / Pvp, since, battle id, NPC name or former owner with id) and
  `TrainerPage.Sightings` (`SightingInfo`), both optional so an older file
  still reads. Boss catches have no battle id: the fight is the won boss fight
  against that species at `CaughtAt`; a PvP specimen's fight is the latest PvP
  win with it as loot, the former owner the losing side of its log (name at
  the time of the fight; „[gelöscht]“ without id when deleted). The box shows „Verlauf“ below the base
  values or the seen sentence: one row per specimen plus „Zuletzt gesehen“; a
  seen species „Zuerst / Zuletzt gesehen“ or „Gesehen“ for one fight. Dates
  link their fight, the former owner their trainer page.
- Checked in the preview (sample trainer 1001, 1280 px, 800 px, 375 px, light
  and dark): Taubsi with three rows fits the box without scrolling at 1280 px;
  at 800 px the box scrolls. Link contrast on the grey rows was 4.0 : 1 with
  `--accent` → text colour with underline (≥ 15 : 1). No console errors.
- User decisions: „von X gewonnen“ with the former owner's name — build it
  (2026-11-27).
- Pitfalls: Razor drops the space between two inline elements, so „Starter“
  and the date ran together (`Starter10.11.2026`) → `dl` with dt/dd. `Battle`
  is a class with init-only properties — no `with`. Port 5180 of the preview
  was taken by another session: a temporary launch entry, not committed.
- User decision (2026-11-27): version 1.11.0 — a feature with a database
  migration. Everything unreleased on `main` (listed under 1.10.1 until then)
  goes out with it; WP-09 and WP-12 move to 1.12.0 and 1.13.0 (proposal).
- Code review (2026-11-27): user decision „Ja, beide so“ (fix both, the former
  owner by the name of the fight).
  - M1 fixed: „Von X gewonnen“ showed the former owner's current name, but a
    rename re-queues only the renamed trainer's page, so the winner's page went
    stale. Now the losing side's owner in the PvP fight's log (side by
    `Outcome`), as on the fight page; a deletion already rewrites the log and
    re-queues the winner. Test renames the former owner after the fight (fails
    on the old code with the new name).
  - L1 fixed: the heading rule `p + h4` also moved „Basiswerte“ (after
    `p.dex-types`) 2 px down; narrowed to `p.muted + h4`, checked in the
    preview.
  - Pre-existing, not made worse: a manipulated date near
    `DateTimeOffset.MaxValue` in a data-host file makes `Format.Date` throw and
    breaks the trainer page (also for the older date fields).
- Deliberately not done: base values as a 2×2 grid to save more height
  (the box scrolls on narrow screens or with many duplicates instead).
