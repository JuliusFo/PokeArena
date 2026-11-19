# WP-05 — NPC fights (2026-10-21, branch wp/05-npc-fights)

- Built: `NpcService` + `!challenge <NPC>` (name or id, case-insensitive) —
  unlock check against `AchievementUnlock`, cooldown per trainer and NPC in
  the `Cooldown` table (`npc:<trainer>:<npc>`, 48 h after a loss, 60 min
  after a win, skipped in the dry run), team size check, one seed for the
  trainer's team of 6, the hits and the reward specimen, `TeamBattle` against
  the fixed team × strength; battle, tokens, badge and reward specimen, the
  achievements it reaches and the cooldown in one transaction. `NpcSettings`;
  `npcs.json` field `tokens` (Eich 1 … Ash 17); `BattleKind.Npc`,
  `Battle.NpcId` / `Tokens`, `Trainer.Tokens`, `NpcBadge`,
  `SpecimenOrigin.Npc` (migration `NpcFights`). UI: NPC rows in "Kämpfe"
  („Ash gegen Eich“, reward or „Eich gewinnt“), columns „Orden“ and „Tokens“
  in the trainer list, dry-run NPC picker „Freischalten“ that tops a test
  viewer up to the NPC's threshold and unlocks the achievements. Tests:
  150 unit, 23 bUnit.
- User decisions: E22 on 2026-10-21 — only the first win brings a reward:
  badge, tokens staggered per NPC in `npcs.json` and a team specimen the
  trainer lacks (first built as tokens for every win, corrected the same
  day); redeeming tokens later. Plan agreed with the proposals (rising token
  amounts, cooldowns kept, plain balance, reply drafts).
- Addenda: spec §1 (badge, tokens), §7 (reward, dry run, reply drafts), §10,
  §14 (E22 closed); architecture (data model, NPC flow); backlog (token
  redemption, ledger, balance command).
- Pitfalls: none new.
- Live test 2026-10-27: dry run — test viewer unlocked for Eich via the NPC
  picker, ten fights against Eich, all won: the first with badge, Hornliu and
  1 token, the other nine without reward. A loss did not come up in the dry
  run (covered by unit tests). Eich looks easy; user decision: leave it for
  now (roadmap question).
- Deliberately not done: redeeming tokens, a token or badge command in chat,
  badges on the website (WP-06).
