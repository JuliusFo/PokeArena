# „Schaden“ box on the fight page — user request (2026-11-10, branch feat/damage-meter)

- Built: Core `BattleLog.DamageDealt()` (damage that arrived per fighter: a
  hit counts the target's HP before it minus after it), written by
  `SiteGenerator` into the new optional `BattleFighter.Damage` (Contracts; no
  format version bump, ADR-0009). Web: `Services/DamageMeter` builds the board
  (side totals; ranking of the trainers in a boss fight with their share of
  the boss's HP, of every Pokémon sent in otherwise; equal damage shares a
  rank), `Shared/DamageBox.razor` renders it between `.sides` and
  „Kampfverlauf“; `Tint.IsColor` / `Tint.TextOn` (black or white on a bar).
  Sample data carries damage and chat colours. Tests: Core (overkill, sides
  add up), DamageMeter, Tint, bUnit page. Spec §9 note ⁴, roadmap,
  CHANGELOG, „Neuigkeiten“ 1.6.0.
- User decisions: mockup 1 (totals + ranking) accepted with two changes —
  „Balken in der Farbe des Twitch-Users“ and „den Text vor den Balken machen,
  wie bei den HP“; „Ja, so bauen“.
- Addenda: Proposal (not confirmed) — see roadmap.md (colour fallbacks, raw
  colour in the dark theme, ranking per kind, version).
- Pitfalls: text inside the bars needs `Tint.TextOn`, not white: the raw chat
  colour may be yellow. Never derive damage from the German log text — it is
  in the stored events.
- Deliberately not done: damage taken, healing, per-round chart.
