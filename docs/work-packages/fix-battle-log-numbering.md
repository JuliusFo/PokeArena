# Battle log numbering — user report (2026-11-10, branch fix/battle-log-numbering)

- Built: `.log li.round` is `display: block` instead of a list item with
  `list-style: none`, so the „Runde N“ headings no longer take a number in
  the website's `<ol class="log">`. Checked in the preview (kampf/1: 1–4).
  CHANGELOG, „Neuigkeiten“ 1.3.1.
- Pitfalls: `list-style: none` only hides the marker — the element still
  increments the `list-item` counter. The desktop log has no numbers.
