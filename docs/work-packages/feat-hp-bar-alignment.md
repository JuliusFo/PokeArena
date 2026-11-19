# HP bar alignment — user request (2026-11-10, branch feat/hp-bar-alignment)

- Built: the „HP left / max HP“ text next to a survivor's bar (`.hp-text`)
  has a fixed, right-aligned width (11.5ch, up to „999 / 999 HP“), so all
  bars of a fight page start at the same x. Checked in the preview
  (kampf/1 with injected rows like „9 / 44 HP“, 800 px and 375 px).
  CHANGELOG, „Neuigkeiten“ 1.3.1.
- Pitfalls: `.end-state` is pushed right with `margin-left: auto`, so any
  width change of the text moved the bar before.
- Deliberately not done: user name colours and boss type colour in the fight
  log (evaluated in the same session; need structured log lines and a
  contrast clamp — separate branch).
