# WP-04 — PvP (2026-10-19, branch wp/04-pvp)

- Built: `Trainer.Login` (refreshed with the display name, migration
  `TrainerLogin` pre-fills the lower-cased display name) and
  `TrainerService.FindByNameAsync` (login first, then display name);
  `PvpService` + `!fight` — challenge in memory (expires lazily after
  2 minutes, dropped on a database switch via a generation counter), accept
  with the same command, requirements checked again, one seed for both teams
  of 6, hits and loot, `TeamBattle`, loot transfer (never the starter) and
  the winner's achievements in one transaction; `PvpSettings`;
  `BattleKind.Pvp`, outcomes `ChallengerWon` / `OpponentWon`,
  `Battle.LootSpecimenId` / `LootSpeciesId` (migration `PvpBattles`). UI:
  PvP rows in "Kämpfe" („Ash gegen Misty“, winner and loot); dry-run button
  „Mit 6 Pokémon ausstatten“ (`DryRunTrainers`, runs on the command queue,
  sandbox only). Tests: 133 unit, 18 bUnit.
- User decisions: plan agreed 2026-10-19 with all five proposals (login
  column, in-memory challenge, DB columns, reply drafts, PvP rows in the
  battle list); 2026-10-20: a dry-run button that tops a test viewer up to
  six Pokémon, because reaching six through boss fights made testing slow.
- Addenda: spec §6 (mention matching, in-memory challenges, achievements by
  loot, reply drafts) and §10 (open challenges in memory); architecture (data
  model, PvP flow, no `OpenChallenge` table).
- Pitfalls: see `src/PokeArena.Core/CLAUDE.md` (database switch fires on the
  UI thread → generation counter; mentions by login) and
  `src/PokeArena.UI/CLAUDE.md` (`@@` in Razor markup). The result reads „…
  bekommt Karpador von Misty“ instead of the planned „dessen Karpador“, which
  assumes a gender.
- Live test 2026-10-20: dry run — two test viewers equipped with the new
  button, challenge and accept, fight resolved with loot transfer. User
  decision: no separate channel test.
- Deliberately not done: pair cooldown (spec §6), withdrawing or declining a
  challenge, the PDF's pick-your-team mode, battle link (WP-06).
