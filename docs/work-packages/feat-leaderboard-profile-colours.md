# Leaderboard pictures and chat colours — user request (2026-11-11, branch feat/leaderboard-profile-colours)

- Built: Contracts `LeaderboardEntry.Color` / `.ProfileImage` and
  `TrainerPage.Color` (optional, left out of the JSON when null — a few hundred
  leaderboard rows; no format bump). `SiteGenerator` fills them; the colour-only
  branch of `TrainerService` now queues `ForTrainers` (leaderboard, statistics,
  meta, trainer page) instead of the statistics alone; `ProfileImageRefresher`
  queues the leaderboard once per batch when a picture appeared, changed or
  went; an unknown picture format counts as none instead of throwing.
  Website: a 24 px circle before every name with the picture inside it
  (`loading="lazy"`, hidden on a load error so the empty circle shows), rows
  keyed by trainer id, the tint on the leaderboard link itself (the underline
  takes the colour), trainer heading tinted with its bold weight kept;
  `SiteData.ProfileImageUrl` takes a path. Tests: generator (colour, picture,
  nulls left out, unknown format), refresher (leaderboard follows a new and a
  lost picture), colour change queue, bUnit (leaderboard pictures, foreign
  host and invalid colour refused; trainer heading). Spec §9 ⁷, architecture,
  ADR-0003 and ADR-0008 addenda, CHANGELOG, „Neuigkeiten“. After updating the
  streamer clicks „Alles neu veröffentlichen“ once: trainer pages are written
  only on a change, so idle trainers would never get their colour.
- User decisions: scope „Ohne Kampfliste“ (fight lists stay plain → backlog).
- Addenda: released as 1.7.1 (user decision at the release); live test passed.
- Pitfalls: `.tinted` sets weight 600 — on the `h1` it would thin the heading
  (`h1.tinted` keeps 700). Sample pictures were generated for the preview and
  removed again (`RepositoryTests`, ADR-0008); the sample data carries colours
  only. Dark theme: darkened names reach about 3.2 : 1 on the table — the open
  1.6.1 test, now more visible.
- Deliberately not done (→ backlog): colours in the fight lists, pictures on
  fight pages, an initial as placeholder.
