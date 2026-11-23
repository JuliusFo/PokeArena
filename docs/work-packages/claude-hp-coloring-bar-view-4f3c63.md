# Coloured HP in the bar view — viewer feedback (2026-11-23, branch claude/hp-coloring-bar-view-4f3c63)

- Feedback on 1.9.0: colour the HP in the „Balken“ view lightly, like in „Text“.
  Evaluated first: the bar view left the attacker's HP in the sentence („HP 90“)
  plain, and its bars had only green and red, while the text view has an orange
  step at half. Proposed A (colour the attacker's HP) and B (orange bars).
- Built: `Battle.razor` shares the steps between bars and text (`Level`:
  low ≤ ¼, mid ≤ ½, high above; `BarLevel` gives the bar's „ low“ / „ mid“).
  `AttackerHp` reads an `IsHp` part (number, fighter the page has, not above
  its maximum); `TextParts` and the bar view's `Parts` both use it. The bar
  view colours it only where `TargetOf` accepts the hit (`withoutTargetHp`),
  so a line the page cannot draw keeps its sentence as written — like the
  text view, whose fallback goes through `Parts` too. Core writes `IsHp` only
  into hit lines. site.css: `.hp-bar.mid` with `--hp-mid: #a35200` (the text
  view's orange; 5.6 : 1 with the white numbers, same fill in both themes like
  green and red). Tests: bUnit for the coloured attacker HP and orange bars in
  the bar view, the half boundary on card and log, the unreadable-HP theory in
  both views; one expectation moved from green to „mid“. Checked in the
  preview: light and dark, an orange hit bar next to the red hatching (set by
  hand in the browser — the sample data has no hit ending between ¼ and ½).
  CHANGELOG, „Neuigkeiten“ (under 1.9.1, proposal), Web CLAUDE.md.
- User decisions: „A + B: auch Orange“ — both variants.
