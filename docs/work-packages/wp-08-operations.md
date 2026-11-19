# WP-08 — Operations (2026-11-04, branch wp/08-operations)

- Built: mod commands bypass the live gate; `!bossreset`; trainer deletion
  (`TrainerService.DeleteAsync` in one transaction, `TrainerRemoval` with the
  same-moderator confirmation for `!entfernen`, inline-confirmed button in the
  trainer list) that overwrites name and id in stored battle logs and queues
  trainer page, lists, fights and co-participants' pages; boss catchers on the
  site read from the log; fight files `max-age=86400`; `DatabaseBackup`
  (`VACUUM INTO` before the migrations on every start and „Jetzt sichern“,
  newest 10); `settings.json` in the data directory as configuration layer
  with the „Einstellungen“ card; `release.yml` (self-contained win-x64 ZIP,
  notes from the CHANGELOG); `UpdateCheck` hint in the top bar; README for
  the streamer; `docs/chat-texts.md` and the reviewed wording. 237 tests
  green.
- User decisions: the 2026-11-04 proposals (mod commands offline, ZIP instead
  of installer, settings card, shorter fight cache instead of a purge, one
  wording pass); confirmation for `!entfernen`; names overwritten in the
  stored logs (ADR-0004 addendum); all six wording proposals accepted.
- Deviations: the backup card shows the folder path instead of an „Ordner
  öffnen“ button (the UI library stays free of Windows APIs); after saving the
  settings the card asks to close and reopen the app instead of offering a
  restart button (it would collide with the single-instance mutex).
- Pitfalls: Core (`VACUUM INTO` from an in-memory database writes into memory,
  so backup tests use a real file in Integration.Tests); Desktop (settings
  layer order; `appsettings.local.json` is a Razor SDK Content item and is
  now excluded from publish).
- Live test 2026-11-04 passed (user, test channel). Released as 1.0.0; the
  first tag also runs the release workflow for the first time.
- Deliberately not done (→ backlog): manual corrections of trainers, backups
  to a second location.
