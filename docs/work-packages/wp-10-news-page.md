# WP-10 — „Neuigkeiten“ page on the website (2026-11-09, branch wp/10-news-page)

- Built: static page `Pages/News.razor` at `/neuigkeiten` (one section per
  version, newest first, points to `!version`), menu link after „So geht's“
  and a sentence in the guide's „Die Website“ paragraph; back-filled 1.1.0
  and 1.2.0. The menu wraps (`flex-wrap`, `nowrap` per link) — with four
  items „So geht's“ broke in two at 375 px. bUnit: versions descending and
  unique, menu links guide and news. Spec §9; `/ship-wp` step 5 and
  `/release` step 2 carry the news line. Checked in the preview (light, dark,
  phone width).
- User decisions: „Leg das als eigenes Arbeitspaket an … schon vor wp-09“
  (WP-10 → 1.3.0, WP-09 → 1.4.0); plan approved with „Ja, umsetzen“: a page
  of its own instead of a CHANGELOG copy, static text, heading per version
  with the date set at release, the page may be ahead of the streamer's app.
- Deliberately not done (→ backlog): entries published from the app via R2
  so they always match the running version.
- Code review (2026-11-09): „Alle fixen“ — six L findings, all fixed:
  - L1 fixed: `/release` took the missing news lines from `[Unreleased]`
    after emptying it; now from the new `[X.Y.Z]` section.
  - L2 fixed: the news line was only in `/ship-wp`; now also in the review
    checklist and in CLAUDE.md §7 „Done means“ (user decision).
  - L3 fixed: Web pitfall claimed a fifth menu item wraps; four already do.
  - L4 fixed: „Auf jeder Kampfseite“ → „Auf den Kampfseiten“ (old fights
    show the end state only after a republish).
  - L5 fixed: the hint throttle is per command; „schneller“ overstated.
  - L6 fixed: news test trims headings, names a bad one and requires a date
    on every section but the newest.
