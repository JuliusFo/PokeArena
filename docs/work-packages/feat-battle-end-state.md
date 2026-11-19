# Battle end state — streamer request (2026-11-08, branch feat/battle-end-state)

- Built: `BattleFighter` gains optional `HpLeft` and `Used` (no format bump,
  ADR-0009); `SiteGenerator` fills them from the log (`BattleLog.HpLeft`,
  `BattleLog.WasUsed` — `SentOut` events in team fights, everyone in a boss
  fight); the fight page shows „X / Y HP“ with a bar (red at a quarter or
  less), „besiegt“, „nicht eingesetzt“, or the start HP for older files.
  Sample data: fight 2 (team fight, was listed but missing) and 3 (escaped
  boss). Spec §9, CHANGELOG; Core and bUnit tests; checked in the preview
  (light, dark, phone width).
- User decisions: built from a spike („Ja, ausbauen“), HP bar included.
- Deliberately not done: end state in the desktop list of fights (headline
  and result only) and in the OBS overlay.
