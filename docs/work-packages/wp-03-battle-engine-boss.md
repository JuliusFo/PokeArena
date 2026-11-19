# WP-03 — Battle engine + boss (2026-10-15, branch wp/03-battle-engine-boss)

- Built: `TypeChart` (`data/typen.json`) and `NpcCatalog` (new `data/npcs.json`,
  17 NPCs with threshold, team as slugs, strength 1.0) validated at startup;
  all `data/*.json` shipped with app and tests. Engine in `Game/Battles/`:
  `DamageCalculator` (ADR-0006 formula, round half away from zero, minimum 1),
  `BossBattle`, `TeamBattle` (1-vs-1 with teams for WP-04/05), `BossDraw`,
  structured `BattleLog` (JSON in the DB, German lines rendered),
  `BattleSettings`/`BossSettings` with the spec defaults, `ISeedSource` +
  `BattleRandom`. `ChatInbox` carries `QueuedWork`; `BossEncounter` with lobby,
  countdown, fight, catches, achievements and cooldown in one transaction;
  `!boss`. Tables `Battles`, `BattleParticipants`, `AchievementUnlocks`,
  `Cooldowns` (migration `BossEncounters`). UI: "Kämpfe" area with list and
  log. `tools/BalancingSimulator.cs`. Tests: 108 unit, 15 bUnit.
- User decisions: plan agreed 2026-10-15 (boss flow as in spec §4), with it
  the four proposals: code defaults for balancing, no cooldowns in the dry run,
  throttled cooldown reply, no battle link before WP-06. `Scaling` 20
  instead of 25 (2026-10-19).
- Addenda: ADR-0006 (simulator results 2026-10-15; `Scaling` 20 on
  2026-10-19); spec
  §4 (reply drafts, cooldown reply, dry run) and §13 (code defaults for now).
- Pitfalls: see `src/PokeArena.Core/CLAUDE.md` (timed steps via `QueuedWork`,
  draw order per seed, `PublishAot=false` for file-based tools). In PowerShell
  scripts `R` is an alias of `Invoke-History` — don't name helpers `R`.
- Live test 2026-10-19: dry run with four simulated viewers — registration,
  too-few lobby, boss fights won and lost, catches, "Kämpfe" view, switching
  back to the live database. User decision: no separate channel test.
- Deliberately not done: runtime balancing editor (roadmap question),
  battle link in chat (WP-06), publishing (WP-06), `!bossreset` (WP-08),
  distinct-species column in the trainer list (not planned).
