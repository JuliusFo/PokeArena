# Website deployed with the release tag — user request (2026-11-10, branch feat/pages-on-release-tag)

- Built: `pages.yml` runs on tags `v*` and by hand instead of on pushes to
  `main` touching the website; path filters are gone (GitHub ignores them on
  tag pushes, so every release redeploys). Setup step for the
  `github-pages` environment tag rule in `docs/website-setup.md` §3,
  ADR-0003 addendum, `CLAUDE.md` §7, `/release` step 6 watches the
  "Website" run of the tag. No CHANGELOG or „Neuigkeiten“ line: neither
  players nor the streamer notice it.
- User decisions: „Pages erst beim Tag“ instead of a long-lived release
  branch — `main` collects website changes and „Neuigkeiten“ lines until the
  release, the integration rule of 2026-10-13 stays.
- Pitfalls: the `github-pages` environment allowed only `main`; without the
  tag rule `v*` the deploy job fails with "not allowed to deploy to
  github-pages". A manual run must pick the release tag under "Use workflow
  from" — run from `main` it publishes unreleased changes.
