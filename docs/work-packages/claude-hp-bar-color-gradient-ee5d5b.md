# Gradual HP colours (2026-11-27, branch claude/hp-bar-color-gradient-ee5d5b)

- User request: HP bar and HP text with a gradual colour instead of three fixed
  steps (green / orange / red).
- Built: `Services/HpColor.cs` — `Fill` (bar fill under the white numbers,
  both themes, and HP text on the light page), `OnDark` (HP text on the dark
  page), `TextStyle`, `Share` (0 … 1 in `double`, a maximum ≤ 0 counts as 1
  like the bar's width). Anchors are the former steps' colours: red up to ¼,
  orange at ½, green from ¾; in between mixed in OKLCH (hue the short way
  round). `Battle.razor` sets `--hp-color` on every bar and `--log-hp` /
  `--log-hp-dark` on `.log-hp`; `Level` / `BarLevel` and the classes
  `.hp-bar.mid/.low`, `.log-hp.high/.mid/.low` are gone. site.css keeps
  `--hp-fill` as fallback and `--log-hp-low` for the text view's damage.
- Tried first: mixing on OKLab's straight line — between orange and green the
  shades came out a greyish brown (#7f6828 at 70 %, measured with the first
  variant's green at full HP); OKLCH keeps the saturation (#826900, olive, same
  point). Both compared as strips in the preview. With the final anchors the
  lowest contrast is 5.0 : 1 with white, 6.9 : 1 on the dark surface.
- Tests: `HpColorTests` (anchors, > 100 shades between ¼ and ¾ in small
  steps, 4.5 : 1 for every shade of 0 … 1000 against white numbers and both
  `--surface` values read from site.css, the anchors equal to `--hp-fill`,
  `--log-hp-low` and `--damage` in site.css, `Share` edge cases incl.
  int.MaxValue);
  `BattleTests` assert colours instead of classes (¼ exactly red, ½ exactly
  orange, one HP more already different).
- Checked in the preview: fight 1, bars light, text view dark.
- User decisions: „Grün schon ab ¾“ — first built with green only at full HP,
  where a bar at 85–90 % was already a little olive; now green from ¾ up
  (fight 1: 94 % and 87 % green, 44 % orange-red).
- Code review (2026-11-27): „Alle“. Second pass on the fixes without findings.
  - L: Web `CLAUDE.md` still named `--hp-low`, removed by this branch — fixed,
    it names the bars' red (#c42d0b).
  - L: the anchors live in C# and site.css too (`--hp-fill` / `Bar.Mixed`,
    `--log-hp-low`, `--damage`) with nothing tying them — fixed,
    `HpColorTests` compares them (fails with the green anchor changed);
    pitfall line in the Web `CLAUDE.md`.
  - L: log and Web `CLAUDE.md` quoted percentages from the first variant —
    fixed, marked as measured with green at full HP.
