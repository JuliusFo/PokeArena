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
  (`.hit-damage`, the damage that arrived, `Before − After` — `Damage` only
  switches the new look on; hidden „ Schaden, “ for screen readers) and the bar; a
  file without damage (1.8.1) or with a negative one keeps the 1.8.1 look.
  Sample fight 1 follows the format. Tests: Core spans and the short
  sentence, generator parts and JSON, bUnit for the short sentence, the
  number, the 1.8.1 file and a foreign damage. Spec §5, CHANGELOG,
  „Neuigkeiten“ (under 1.8.2, proposal).
- User decisions: mockup „A“ (damage moves to the bar) over „B“ (one HP
  column per side) and „C“ (bar inside the sentence) — „A umsetzen“. The
  number is the damage that arrived, like the bar and the „Schaden“ box (a
  knockout of 77 on 70 HP left shows „−70“) — „Nr. 1 anrechnen“ in the
  code review.
- Addenda: version 1.8.2 is a proposal (not confirmed). Superseded
  2026-11-27: the number is the hit's full damage again, after viewer feedback
  (claude/damage-display-capping-25b0a0).
- Pitfalls: the dark theme's `--hp-low` is under 3 : 1 as text on the dark
  surface, so `.hit-damage` uses a lighter red there.
- Deliberately not done (→ backlog): one HP column per side (mockup „B“),
  a target-coloured edge on the bar, „Volltreffer“ on the number.
- Code review (2026-11-20): „Alle, Nr. 1 anrechnen“.
  - M: the number was the full damage, beside a bar and a „Schaden“ box that
    count what arrived (−77 next to 70 → 0) — fixed, the page shows
    `Before − After`, bUnit test on the knockout.
  - L: the „Neuigkeiten“ example left out the attacker's HP — fixed.
  - L: the phone-width sentence column is about 40 px narrower — not
    reproduced: the longest boss line takes 5 lines at 375 px, as in 1.8.1.
  - L: no test for a hit with damage the page cannot draw — fixed (theory
    data carries `Damage` and `TextWithBar`).
  - Second pass: the log lacked the decision above — fixed.
