# HP colours over the whole range (2026-11-28, branch claude/health-bar-gradient-166dce)

- Viewer feedback on 1.11.0: the „gradual“ HP colours read as a few fixed
  colours. A friend of the user sent a mockup (screenshot of the text view,
  dark theme) where every HP number had its own shade, the hue running evenly
  from green over yellow to red. Measured from the screenshot: 351/351
  #1cb741, 174/351 (≈ 50 %) #bab13c, 64/351 #be6131 — ours gave the same green
  from 75 % up, the same red up to 25 %, and orange (#f0a04b) at half.
- First misunderstood as a gradient inside one bar (mockups of four bar
  variants); the user meant the colour per HP value. The friend's CSS snippet
  (`linear-gradient(… ${hp}%, … ${hp}%)`) is a hard edge, not a gradient: the
  same as our `.hp-bar`, only with a dark red track.
- Built: `HpColor` anchors at 0 (red), ½ (yellow) and full (green), mixed in
  OKLCH as before. Red and green anchors unchanged (`--hp-fill`, `Bar.Mixed`,
  `--log-hp-low`, `--damage` stay tied); the middle went from orange to yellow:
  `#8a6d00` (dark ochre) for fills and light-page text, `#e2c044` on the dark
  page. Lowest contrast along the way: 4.89 : 1 with white, 6.86 : 1 on the dark
  surface. The friend's own red-orange (#be6131) would be ≈ 3.7 : 1 on our dark
  surface, so the dark red stays #ff8a6e.
- Tests: `HpColorTests` (anchors, only 0 red and only full green, > 300
  distinct shades over 0 … 1000 in steps ≤ 6 per channel for both palettes,
  4.5 : 1 as before); `BattleTests` adjusted (a fifth and a quarter left are no
  longer the full red, half is the yellow anchor).
- Checked in the preview: fight 1, text view dark (90/90 green, 541/576
  yellow-green, 40/90 yellow, 18/78 orange, 0 red) and bars light.
- User decisions: „Ja, umbauen“ (2026-11-28) — the whole range moves; this
  replaces „Grün schon ab ¾“ of 2026-11-27, so 85–90 % now has a slight yellow
  tint.
- Version: News section „Version 1.11.1“ (proposal, not confirmed).
