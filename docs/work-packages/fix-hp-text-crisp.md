# Crisp HP numbers — user report (2026-11-10, branch fix/hp-text-crisp)

- Built: the HP numbers inside the bar are two stacked copies instead of one
  text with `background-clip: text`: `.hp-text` in `--text`, clipped to the
  empty part (`clip-path: inset(0 0 0 var(--hp))`), and `.hp-text.on-fill` in
  `--hp-on-fill` on top, clipped to the fill, `aria-hidden` and
  `user-select: none`. Same hard edge at `--hp`, same colours in both themes.
  bUnit test checks both copies. CHANGELOG, „Neuigkeiten“ (next release, 1.6.0 since feat/damage-meter).
- User decisions: a friend's screenshot comparison showed the numbers thin and
  grey with `-webkit-text-fill-color: transparent` and crisp with `white`;
  plain `white` would be unreadable over the light theme's empty part (≈ 1.2 : 1),
  so „Zwei Textebenen“.
- Pitfalls: `background-clip: text` loses subpixel antialiasing in Chromium.
  The two copies must not overlap: the dark copy's antialiased edges would
  show as a dark fringe around the white glyphs over the fill.
