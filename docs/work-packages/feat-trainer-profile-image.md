# Profile picture on the trainer page — user idea (2026-11-11, branch feat/trainer-profile-image)

- Built: `ProfileImageRefresher` (Core, live database, every 15 min while
  Twitch is connected) asks Helix `GET /users?id=` (new
  `HelixClient.GetProfileImageUrlsAsync`, 100 ids per call) for trainers
  without a picture or with one not checked for 24 h, downloads only changed
  URLs (150×150 variant, fallback to the reported one; https,
  `static-cdn.jtvnw.net` only, PNG/JPEG, ≤ 512 KB) into the new table
  `TrainerProfileImage` and queues `profilbild/<id>.png|.jpg` plus the
  trainer page. `SiteFile` carries a content type for R2; `SiteGenerator`
  renders the picture (cached a day); deletion, „Alles neu veröffentlichen“
  and the bucket listing cover the prefix. Contracts: `SitePaths.ProfileImage`
  / `ProfileImageOf`, optional `TrainerPage.ProfileImage`. Website: round
  56 px picture left of the name, only for paths in the bot's format on the
  data host; privacy page updated. Tests: refresher
  (new, fallback, daily check, format change, account gone, refusals, batches,
  deletion, republish, path parsing), bUnit (data host only). Spec §9 ⁵ and
  §11, architecture.md, ADR-0003 addendum, CHANGELOG, „Neuigkeiten“ 1.7.0.
- User decisions: „Gleich planen“ instead of the backlog; „Lass uns das Bild
  auf unseren r2 packen“ (mirror, not Twitch's CDN); „Mach das ruhig jetzt“
  — built before the „Statistik“ page as 1.7.0 (Statistik → 1.8.0, WP-09 →
  1.9.0). Merge order: `feat/statistics-page` was merged first (also
  as 1.7.0), so both ship in 1.7.0 and WP-09 stays 1.8.0 — „Ja, so lassen“.
- Addenda: the image bytes live in SQLite so the file stays derived (ADR-0003);
  the WAF refuses query strings, so no cache busting — a new picture can take
  two days (daily check + a day of cache). Keys got an extension while
  building (the dev server serves no extensionless files).
- Pitfalls: `HttpResponseMessage.RequestMessage` is set by the real handler
  only (redirect check falls back to the requested URL); `is var (a, b)` on a
  nullable tuple would match null — use `is ({ } a, { } b)`. The sample data has
  no picture: `RepositoryTests` (ADR-0008) refuses every image file under
  `src/`, even an invented one — checked once in the preview, then removed.
- Deliberately not done (→ backlog): pictures on the leaderboard and fight
  pages; a placeholder for trainers without a picture.
- Code review (2026-11-11): „Alle, #1 als Nachtrag“; second pass „Ja, alle drei“.
  - M ADR-0008 bans fan drawings, and avatars on a Pokémon channel may be
    such: fixed as the ADR-0008 addendum (viewer content, not a project
    asset); a hide action per picture → backlog.
  - M picture URLs in the log (older ones contain the login; the privacy page
    promises ids only): fixed, logs carry `{UserId}` only.
  - L body read unbounded after `ResponseHeadersRead`: fixed, 30 s per download
    via a `TimeProvider` token.
  - L type taken from the header only: fixed, PNG/JPEG signature decides.
  - L trainers without a picture retried every 15 min: fixed, 24 h in memory;
    second pass: 408/429/5xx count as unreachable (next round), not refused.
  - L `OpenLive()` docs named only the publish worker: fixed (Core CLAUDE.md,
    checklist, doc comment).
  - L `onerror`-hidden `<img>` reused for the next trainer: not reproduced —
    the page shows „Lädt …“ between two trainers and builds a new element.
  - L test gaps (redirect, service loop, deletion during a round): fixed; second
    pass made the deletion test cover the backoff-after-save and the loop test
    robust against a lost advance.
