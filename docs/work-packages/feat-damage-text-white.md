# White text in the „Schaden“ bars — user request (2026-11-10, branch feat/damage-text-white)

- Built: `Tint.Darkened` (replaces `Tint.TextOn`); `DamageMeter` bar fills
  use it, `.damage-text.on-fill` is always white like `.hp-text.on-fill`.
  `Tint.Style` gives that one colour (`--tint`) for both themes; the
  lightened dark-theme variant and `Tint.DarkSurface` are gone. Tests: Tint
  (darkened fills, name = bar colour, AA on white), DamageMeter, bUnit page.
  Spec §9 notes on the log colours and ⁴, Web pitfalls, CHANGELOG 1.6.1,
  „Neuigkeiten“ 1.6.1.
- User decisions, in order: „Text-Farbe für den Damage-Meter wie bei den HP“;
  names and bars „die selben Farben“; tried per-theme variants with dark text
  in the dark theme, then raw colours everywhere with white text („zum Test
  alles auf die tatsächliche Farbe“, „trotzdem weiße Schrift“, PR #47 opened
  for it); „tatsächlich die abgedunkelte Version, wie vorher die Namen“ —
  bars darkened with white text; final: „Mach bitte die Balken-Farbe zum
  Standard. Wir testen, ob man die Namen dann noch gut lesen kann.“ — names
  in the bar colour in both themes.
- Addenda: one colour cannot carry white text and read on `#222228`
  (luminance ≤ 0.18 vs ≥ 0.25), so dark names (blue `#0000ff` ≈ 2.4 : 1) are
  hard to read in the dark theme — that is what the streamer test checks.
  Going back for names means restoring the `--tint-dark` variant of 1.6.0.
- Pitfalls: yellow turns olive (`#737300`), white grey (`#737373`).
