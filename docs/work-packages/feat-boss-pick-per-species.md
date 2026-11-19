# Boss pick per species — user request (2026-11-09, branch feat/boss-pick-per-species)

- Built: `BossDraw.FieldedSpecies` draws uniformly over the distinct species
  a participant owns (sorted by id, so a seed keeps its meaning);
  `BossEncounter` uses it. Simulator part 3 compares specimen and species
  pick on the same seed (same boss, same collections). Spec §4, guide,
  „Neuigkeiten“ 1.3.1, CHANGELOG; Core test (99 × Digda + Arkani ≈ 50 %).
- User decisions: „Ich würde es gerne jetzt haben“ (ahead of WP-09, with a
  simulator run); after the run showed no average effect: „Trotzdem
  ausliefern“.
- Addenda: finding in roadmap.md WP-09 — the common share comes from the
  boss tier odds, not from duplicates.
- Deliberately not done (→ backlog): the same rule for PvP/NPC teams
  (backlog „Draw teams without duplicate species“).
