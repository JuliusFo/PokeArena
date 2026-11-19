# Stylesheet cache busting — user report (2026-11-10, branch fix/css-cache-busting)

- Built: `pages.yml` appends the first 12 hex digits of `css/site.css`'s SHA-256
  as `?v=` to the stylesheet link in `index.html` (before the copy to
  `404.html`, stale `index.html.br/.gz` removed; fails if the link is missing).
  CHANGELOG. No „Neuigkeiten“ line: players only notice the bug going away.
- User report: after 1.6.0 Firefox showed the HP numbers twice, one above the
  other — the cached 1.5.0 `site.css` (one text with `background-clip: text`,
  no `position: absolute` for the copy) next to the new markup from the hashed
  DLL (two copies). Chrome had revalidated.
- Pitfalls: `#[.{fingerprint}]` in `index.html` only resolves for JS modules.
  `StaticWebAssetFingerprintPattern` for `*.css` renames the published file to
  `site.<hash>.css` but leaves the link as `css/site.css` — a 404 on Pages.
