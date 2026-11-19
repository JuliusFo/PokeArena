# „Zurück“ keeps the scroll position — streamer feedback (2026-11-19, branch feat/back-keeps-scroll)

- Built: an inline script in `wwwroot/index.html` (before Blazor). Cause: on
  back/forward Blazor renders the page anew and the page loads its JSON first,
  so the browser's own restoring found only „Lädt …“ and clamped to the top.
  The script sets `history.scrollRestoration = 'manual'`, gives every history
  entry an id in `history.state` (spread next to Blazor's `_index` and
  `userState`), remembers its position on scroll, input and `pagehide`
  (memory + `sessionStorage`, last 100 entries) and after `popstate` or a
  reload applies it on every resize of `body`, clamped by the browser, until
  the visitor scrolls, clicks or types, another page opens, or 15 s pass; an
  entry without a position goes to the top at once (with manual restoring
  nobody else does). A link click still opens the next page at the top
  (Blazor's own reset, only when the path changes). Checked in the preview:
  list of fights back and forward, with the data fetch delayed by 1.2 s,
  reload, the menu link from a fight page to the list goes to the top, back to
  a page that was at the top. No automated test: bUnit does not run the
  page's JS, and UI automation is out (`CLAUDE.md` §4).
- User decisions: —
- Addenda: version 1.7.2 was a proposal; decided at `/release`: released as 1.8.0 (2026-11-19).
- Pitfalls: in a hidden browser pane no frames render, so scroll events and
  `ResizeObserver` wait — remember the position on input and `pagehide` too,
  not only on scroll.
- Deliberately not done (→ backlog): —
