# Pokédex search: focus frame, click outside — user feedback (2026-11-24, branch claude/pokedex-search-ui-fixes-79cb0c)

- Feedback: closing the search with Escape left a frame around „Name“; a click
  on free space should close the search too.
- Cause of the frame: `CloseSearch` moved the focus back to „Name“ by script.
  A focus moved by script right after a key press matches `:focus-visible`, so
  the browser draws the keyboard focus ring — also for a mouse user who only
  pressed Escape.
- Built: `OpenSearch` notes whether „Name“ was activated by keyboard (Enter or
  Space fire a click with `detail` 0, a mouse or tap from 1); only then does
  closing move the focus back. A small script in `index.html`
  (`pokeArenaDex.watchOutside` / `.unwatch`) watches clicks while the search is
  open: a click outside the field that hits no control (`a, button, input,
  select, textarea, label, summary, [tabindex]`) calls `CloseFromOutside`
  (clears the query, no focus move). `click`, not `pointerdown`: scrolling on a
  phone must not close it; a click that started in the field counts as inside.
  Clicking a row or „Alle / Nur gefangene“ keeps the search and its results.
  The component unwatches on close and in `DisposeAsync`; without the script
  (old cached `index.html`) the `JSException` is swallowed and „×“ / Escape
  still work. `WebTestContext` plans the two calls. bUnit tests: mouse-opened +
  Escape moves no focus to „Name“, a callback from the watch closes the search
  and shows all 151 rows; the keyboard focus test now clicks with `detail` 0.
  Checked in the preview: Escape after a mouse click leaves the focus on the
  page without a ring, a row click keeps the search, a click beside it closes
  it, Enter + Escape returns the focus to „Name“. CHANGELOG, „Neuigkeiten“
  (under 1.10.1, proposal), spec §9, Web `CLAUDE.md`.
- Pitfalls: bUnit's `Click()` sends `detail` 1 (a mouse click) by default;
  pass `new MouseEventArgs { Detail = 0 }` for a keyboard activation. bUnit
  renders `blazor:elementReference` empty after the first render of an element,
  so a test cannot compare a later JS argument with it.
- Open question (not blocking): a click on free space also clears a typed
  query, like Escape. If that loses searches by accident, the click could close
  only an empty field.
