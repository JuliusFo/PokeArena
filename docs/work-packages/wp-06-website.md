# WP-06 — Website (2026-11-01, branch wp/06-website)

- Built: `PokeArena.Contracts` (site records, source-generated JSON,
  ADR-0009). Core `Publishing/`: `SiteOutbox` queues paths in the game
  transaction (registration, rename, boss, PvP, NPC), `SiteGenerator` renders
  `meta`, `rangliste`, `kaempfe`, `trainer/<id>`, `kampf/<id>` from the DB,
  `SitePublishWorker` uploads from the live outbox (2 s poll, backoff 5 s …
  15 min, never gives up, sandbox never published, deletes files whose source
  is gone), `R2SitePublisher` / `R2Access` (AWSSDK.S3, key in the Credential
  Manager), `SiteLinks` + `!arena`; migration `Website` (`PublishQueue`,
  `Specimen.AcquiredAt` with backfill from PvP loot). Desktop: „Website“ and
  „OBS-Overlay“ cards, `OverlayServer` (Kestrel on loopback, port 5299).
  `PokeArena.Web` (Blazor WASM): leaderboard, battle index, battle page with
  404 retry, trainer page by id or name, privacy note, fan disclaimer;
  invented sample data for local runs. Workflow `pages.yml` (Pages deploy,
  `DATA_URL` variable, 404.html fallback); CI runs the website tests. Tests:
  171 unit, 26 bUnit (desktop), 9 bUnit (website), integration: overlay
  server, R2 round trip (skipped until set up).
- User decisions: 2026-10-30 — no `r2.dev` (unbounded per-request cost);
  bucket in the user's Cloudflare account behind a subdomain of the existing
  zone (HorizonNET) with cache and a rate-limiting rule; the site gets a
  subdomain too (ADR-0003 addendum). 2026-11-01 — plan agreed with the
  proposals: shared contracts project, republish-all already in WP-06,
  overlay = latest fight + top 5, fan disclaimer in the footer, tie-break by
  `AcquiredAt`.
- Addenda: ADR-0003 addendum, ADR-0009, spec §9 (custom domain, tie-break
  rule), architecture (publishing, overlay, website), new
  docs/website-setup.md, backlog (custom-domain item closed).
- Pitfalls: Core (queue paths in the same transaction, `OpenLive`,
  `BattleKind` clash), Desktop (System.IO, overlay port and log level), new
  Web/CLAUDE.md.
- Deviation from the architecture sketch: the outbox stores only paths, no
  payload — files are rendered at upload time, so they are always current
  and duplicates collapse.
- Live test 2026-11-01: Cloudflare (bucket behind `arena-data.cloudfox.xyz`,
  cache rule, rate-limiting rule by path — the free plan has no hostname
  field, CORS, account API token) and Pages (`arena.cloudfox.xyz`, verified
  domain, `DATA_URL`) set up by the user. Key entered in the app, „Alles neu
  veröffentlichen“ uploaded the existing data; second request a cache HIT,
  404s not cached (BYPASS), CORS header and `Vary: Origin` present; R2
  integration test green. A real `!boss` in the test channel (two trainers,
  boss escaped) posted the battle link, and `kampf/1.json`, `kaempfe.json`
  and `rangliste.json` were on the data host about a second later. Website
  run locally against the real data host; the Pages deploy follows the merge.
- Deliberately not done: deleting trainers and „[gelöscht]“ in old fights
  (WP-08; the privacy page already describes it), purging cached fight files
  after a deletion (WP-08), overlay content beyond the simplest variant.
