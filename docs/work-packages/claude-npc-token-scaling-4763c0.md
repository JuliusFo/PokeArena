# One token per NPC — streamer feedback (2026-11-19, branch claude/npc-token-scaling-4763c0)

- Built: `tokens` = 1 for all 17 NPCs in `data/npcs.json` (was 1 … 17);
  base data test, spec §7, guide text („einen Token“), CHANGELOG,
  „Neuigkeiten“. No code change — the field stays configurable per NPC.
- User decisions: 1 token per NPC (2026-11-19, from the streamer).
- Addenda: —
- Pitfalls: —
- Deliberately not done: earned balances are not corrected (open question in
  roadmap.md WP-05).
