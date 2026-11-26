# Full hit damage in the fight log again — viewer feedback (2026-11-27, branch claude/damage-display-capping-25b0a0)

- Feedback on 1.10.0: a viewer read „HP 40 → 0 … verursacht 40 Schaden“ as a
  hit of exactly the HP left („unlucky“) — the hit was 210. Until 1.8.1 the
  sentence carried the full hit; 1.8.2 capped the number next to the bar at
  what arrived (code review finding M of
  claude/kampflog-hp-balken-feedback-808390, „Nr. 1 anrechnen“: −77 next to
  70 → 0 looked inconsistent with the bar and the „Schaden“ box), and 1.9.0's
  text view took the rule over.
- Built: the bar view's `.hit-damage` and the text view's `.log-damage` show
  `LogHit.Damage` (the full hit); the text view falls back to `Before − After`
  for a negative (foreign) damage, as the bar view falls back to the 1.8.1
  sentence. No Core or file change: fight files since 1.8.2 carry the full
  damage. Tests: bUnit expectations of the knockout (−31 / 31 on 22 left), new
  fallback test. Spec §5, CHANGELOG, „Neuigkeiten“ (under 1.10.1, proposal),
  Web CLAUDE.md, Contracts doc of `LogHit.Damage`.
- User decisions: full damage in both views („Ja, beide Ansichten“); the
  „Schaden“ box keeps counting only what arrived (the line says how hard a hit
  was, the box what it added to the fight — proposal, not asked separately).
- Pitfalls: none new.
