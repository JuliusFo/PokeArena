# HP numbers inside the bar — user request (2026-11-10, branch feat/hp-text-in-bar)

- Built: a survivor's end state on the fight page is one pill-shaped `.hp-bar`
  (fixed width `12ch + 16px`) with `.hp-text` centred inside instead of a 64 px
  bar plus an 11.5ch text column (≈ 70 px narrower). Fill and text colour share
  one hard gradient edge at `--hp` (set inline): text over the fill in
  `--hp-on-fill`, over the rest in `--text` (`background-clip: text`), so the
  numbers stay one text node. CHANGELOG, „Neuigkeiten“ (planned as 1.4.3, now in the 1.5.0 section together with `!bosscooldown`).
- User decisions: „die HP in den Balken mergen, dass es ineinander angezeigt
  wird“ (save space on the fight page).
- Pitfalls: white text on `--ok`/`--accent` is ≈ 4.4 : 1 in the light theme,
  hence darker fills (`#1b7f45`, `#c42d0b`) only inside the bar; dark theme uses
  `--ok`/`--accent` with dark text. Neither `.hp-bar` nor `.hp-text` may get
  horizontal padding/border on the bar itself, or the two gradient edges drift
  apart.
- Addenda (2026-11-10, branch fix/hp-bar-dark-mode): feedback said the dark
  text on bright green was hard to read in the dark theme (8 : 1 by WCAG, but
  the bright fill glares). Seven variants compared on a throwaway page (shadow,
  outline, bold black, muted fill with coloured edge, darker fills); user
  decision „B ist gut“: the dark theme drops its override and uses the light
  theme's `#1b7f45`/`#c42d0b` with white text (5.0 / 5.6 : 1). No new
  „Neuigkeiten“ line: the next version (now 1.5.0) is unreleased and its bar line
  covers it.
