# Text view for the fight log — viewer feedback (2026-11-21, branch claude/kampflog-design-options-cb7042)

- Feedback on 1.8.2: not every viewer likes the hit bars; the user asked for a
  switch back to the „old“ design, a little improved, and sent a mockup of it
  („… trifft Kokuna (Boss, HP 351 → 323) und verursacht 28 Schaden.“ with
  coloured numbers).
- Built: `BattleLog.Label` splits the attacker's „, HP 24)“ into „, HP “, the
  number (`LogSpan.IsHp`) and „)“; the damage span is marked `IsDamage`.
  Contracts `LogPart.IsHp` / `IsDamage`, left out of the JSON when false. The
  text of every line is unchanged, so desktop lists, `Lines` and chat stay the
  same. Fight page: a switch „Balken / Text“ (`.view-switch`, `aria-pressed`)
  next to the „Kampfverlauf“ heading, only when the log has hits; the pick
  lives in `localStorage` (`pokearena.kampfverlauf`), bars by default. The text
  view (`TextParts`) shows „… trifft Relaxo (Boss, HP 541 → 503) und
  verursacht 38 Schaden.“ with the HP coloured (`.log-hp` high above half, mid
  above a quarter, low at a quarter or less) and the damage that arrived in
  bold red; a file without `IsDamage` keeps its wording with coloured target
  HP. Privacy page: one sentence on the stored pick. Sample fight 1 follows the
  format. Tests: Core spans and generator JSON, bUnit for the text view, the
  switch and its storage, blocked storage, an older file and a log without
  hits. Checked in the preview: light and dark, 375 px, the pick survives a
  reload; every colour ≥ 5 : 1 on the log surface. Spec §5, CHANGELOG,
  „Neuigkeiten“ (under 1.8.3, proposal).
- User decisions: „Ja, loslegen“ on the plan — a per-viewer switch, bars stay
  the default, the number is the damage that arrived (the mockup's 112 on
  64 → 0 shows 64, like the bars and the „Schaden“ box).
- Addenda: version 1.8.3 is a proposal (not confirmed).
- Pitfalls: bUnit's `JSInterop` is strict — every fight page test would fail
  on the `localStorage.getItem` call; `WebTestContext` plans `getItem` and
  `setItem` and stays strict (a test's own later `Setup` wins).
- Code review (2026-11-21): „Alle“.
  - L: `WebTestContext` set bUnit's JS interop loose for every web test, so a
    stray JS call anywhere would pass unnoticed — fixed, strict again with only
    `localStorage.getItem` / `setItem` planned (a test's later setup wins).
  - L: no test for the text view's fallbacks (unreadable attacker HP, a hit the
    page cannot draw) — fixed, theory and fact; removing the `MaxHp` check fails
    the theory.
  - L: comments called the text view's thresholds „like the bars“ although the
    orange step is its own — fixed in Battle.razor, site.css and Web CLAUDE.md.
  - Second pass: all three covered, nothing new. Noted, not changed: the
    blocked-storage test does not prove on its own that the `setItem` exception
    path runs (it relies on bUnit's „later setup wins“, shown for `getItem`).
