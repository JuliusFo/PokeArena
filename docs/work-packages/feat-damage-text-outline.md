# Black outline for the „Schaden“ bar text — user request (2026-11-10, branch feat/damage-text-outline)

- Built: `.damage-text.on-fill` gets a 1 px black outline (`text-shadow` in
  eight directions), both themes; the clip-path cuts it at the fill's edge.
  Spec §9 note ⁴, Web pitfalls, CHANGELOG, „Neuigkeiten“ (next release).
- User decisions: after looking at a screenshot of 1.6.1 „weiße Schrift (wie
  jetzt) und schwarzem dünnen Rand“; mockups B (1 px), C (`text-stroke`),
  then D (1.5 px), E (2 px), F (3 px stroke) — picked „1B (1 px)“.
- Pitfalls: `-webkit-text-stroke` closes the counters of „a“/„e“ at 13 px;
  `text-shadow` keeps the glyphs as they are.
- Deliberately not done: no outline on the copy over the empty rest (normal
  text on the track needs none).
