# Fighter row wrap — user report (2026-11-10, branch fix/fighter-row-wrap)

- Built: on the fight page species and owner sit in `<span class="who">`, a
  wrapping flex box with `flex: 1 1 0`; `.end-state` does not shrink — a long
  name wraps inside `.who`, the HP bar keeps its column on the right. The row
  itself only breaks when even the longest word of `.who` does not fit next to
  the end state (320 px with a large system font). From 1100 px viewport `.sides` reaches 70 px
  past the page's content on each side (868 → 1008 px; `.page` is 900 px
  including its 16 px padding). CHANGELOG, „Neuigkeiten“ 1.4.1.
  Checked in the preview (kampf/1 with injected owners „ShiyoFurball“ and a
  25-character name; 1200 px dark, 375 px light: bars at the same x, no
  horizontal scroll).
- User decisions: fix the wrap and widen only the fight page („Fix +
  Kampfseite breiter“), not every page.
- Pitfalls: `min-width: 0` on `.who` lets the species name run into the end
  state on narrow rows; the basis of 0 keeps the min-content as the wrap
  point. `.page` is `border-box` — its content is 868 px, not 900 px. Widening
  `.page` itself would move the header when switching pages, hence only
  `.sides` reaches out.
- Deliberately not done: at 375 px a 25-character name without spaces breaks
  inside the word (`overflow-wrap: anywhere`) and a second type chip can drop
  under the name — both stay inside the card.
