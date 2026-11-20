# Damage next to the hit bar — viewer feedback (2026-11-20, branch claude/kampflog-hp-balken-feedback-808390)

- Feedback on 1.8.1 (two viewers): one had to look left for the damage and
  right for the HP, and found the bars of the trainers' Pokémon between the
  boss's confusing; the other liked having everything on the right; both
  in the sentence and in the bar would be „doppelt gemoppelt“.
- Built: `HitTarget` and `LogHit` carry the hit's `Damage` (Contracts: null
  in older files). `LogSpan`/`LogPart` get `TextWithBar`, the text where the
  page draws the bar: „ hat “ → „ trifft “, „ 20 Schaden zugefügt.“ → „.“
  (the damage is now its own span, the effectiveness too). Desktop lists,
  `Lines` and chat are unchanged. Fight page: `.hit-numbers` holds „−20“
  (`.hit-damage`, hidden „ Schaden, “ for screen readers) and the bar; a
  file without damage (1.8.1) or with a negative one keeps the 1.8.1 look.
  Sample fight 1 follows the format. Tests: Core spans and the short
  sentence, generator parts and JSON, bUnit for the short sentence, the
  number, the 1.8.1 file and a foreign damage. Spec §5, CHANGELOG,
  „Neuigkeiten“ (under 1.8.2, proposal).
- User decisions: mockup „A“ (damage moves to the bar) over „B“ (one HP
  column per side) and „C“ (bar inside the sentence) — „A umsetzen“.
- Addenda: version 1.8.2 is a proposal (not confirmed). The number is the
  hit's full damage, like the sentence before — on a knockout more than the
  bar loses (77 on 70 HP left).
- Pitfalls: the dark theme's `--hp-low` is under 3 : 1 as text on the dark
  surface, so `.hit-damage` uses a lighter red there.
- Deliberately not done (→ backlog): one HP column per side (mockup „B“),
  a target-coloured edge on the bar, „Volltreffer“ on the number.
