# Pokédex search: focus frame, click outside — user feedback (2026-11-24, branch claude/pokedex-search-ui-fixes-79cb0c)

- Feedback: closing the search with Escape left a frame around „Name“; a click
  on free space should close the search too.
- Cause of the frame: `CloseSearch` moved the focus back to „Name“ by script.
  A focus moved by script right after a key press matches `:focus-visible`, so
  the browser draws the keyboard focus ring — also for a mouse user who only
  pressed Escape.
- Built: `OpenSearch` notes whether „Name“ was activated by keyboard (Enter or
  Space fire a click with `detail` 0, a mouse or tap from 1). Escape moves the
  focus back to „Name“ only then; „×“ always does (its button is gone, a
  screen reader would start at the top). A small script in `index.html`
  (`pokeArenaDex.watchOutside` / `.unwatch`) watches taps while the search is
  open: a press outside the field that hits no control (`a, button, input,
  select, textarea, label, summary, [tabindex]:not([tabindex="-1"])`) and
  moves at most 10 px calls `CloseFromOutside`, which closes an empty search
  only (no focus move). Pointer events, not `click`: iOS Safari sends no click
  to `document` for a tap on an element without its own handler; scrolling or
  selecting text moves more than 10 px or is cancelled. A typed query stays
  with its results (tapping beside the field hides a phone's keyboard). Rows
  and „Alle / Nur gefangene“ keep the search open. The component unwatches on
  close and in `DisposeAsync`; without the script (old cached `index.html`)
  the `JSException` is swallowed and „×“ / Escape still work. `WebTestContext`
  plans the two calls. bUnit tests: „×“ returns the focus after a keyboard and
  a mouse open, Escape only after a keyboard open, a tap closes an empty search
  and keeps one with a query, leaving the trainer unwatches. Checked in the
  preview (synthetic pointer events, the pane was hidden): a tap on the `h1`
  closes, a tap with „ra“ typed keeps 4 rows, a 50 px drag and a row tap keep
  it open, „×“ returns the focus to „Name“. Not checked on a real iPhone.
  CHANGELOG, „Neuigkeiten“ (under 1.10.1, proposal), spec §9, Web `CLAUDE.md`.
- Pitfalls: bUnit's `Click()` sends `detail` 1 (a mouse click) by default;
  pass `new MouseEventArgs { Detail = 0 }` for a keyboard activation. bUnit
  renders `blazor:elementReference` empty after the first render of an element,
  so a test cannot compare a later JS argument with it.
- User decision (code review 2026-11-24, „Alle beheben“): a tap on free space
  closes an empty search only.
