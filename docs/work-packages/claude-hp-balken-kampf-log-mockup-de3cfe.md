# HP bar per hit in the fight log — user pick (2026-11-19, branch claude/hp-balken-kampf-log-mockup-de3cfe)

- Built: `BattleLog.ToText` gives every hit a `HitTarget` (target, HP before
  and after, from the same replay as the „Schaden“ box) and splits the target's
  „, HP 88 → 30“ off its „)“ as a span marked `IsTargetHp`; the text stays the
  same, so desktop lists, `Lines` and chat are unchanged. Contracts:
  `LogLine.Hit` (`LogHit`: side, fighter on the page, before, after) and
  `LogPart.IsTargetHp`, both left out of the JSON when unset — older files stay
  valid and show the full sentence. Fight page: a hit is a row with the
  sentence (without the target's HP) and the fight cards' `.hp-bar` as
  `.hit-bar` on the right: fill up to the HP after, hatched red up to the HP
  before (at least 4 px), „80 → 22“ inside as two clipped copies with a hidden
  „HP“ for screen readers, red at ≤ 25 %. A hit the page
  cannot draw (unknown fighter, HP out of range) keeps the full sentence.
  Tests: spans and hits incl. a hit beyond the HP left, generator positions and
  JSON, bUnit for bars, text and the fallback. Sample fight 1 follows the format.
  Spec §5, CHANGELOG, „Neuigkeiten“.
- User decisions: mockup variant „B“ (target HP from the sentence into the bar,
  the attacker keeps its number) over „A“ (bar next to the unchanged sentence)
  and „C“ (B plus the other backlog ideas); „Ja, loslegen“ on the plan;
  code review: „Alle, 3 mindestenbreite“ — all seven findings fixed, the lost
  piece gets a minimum width (details in the PR).
- Addenda: version 1.8.1 confirmed at `/release` (user pick, 2026-11-19);
  working on the session branch instead of `feat/…` is a proposal (not
  confirmed).
- Pitfalls: Core's hit record (`HitTarget`, log indexes) must not be called
  `LogHit` like the Contracts one (page positions), or `SiteGenerator` sees an
  ambiguous name. „→“ is written as `\u2192` in the generated JSON — tests must
  not search for the arrow literally. Text next to the fixed-width bar needs
  `overflow-wrap: anywhere`, or a long name runs over the bar on a phone.
- Deliberately not done (→ backlog): shorter duel lines, knockout in the hit's
  line, duel sections, effectiveness badges, boss log filter.
