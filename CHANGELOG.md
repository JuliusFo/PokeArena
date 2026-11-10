# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). Each release is
tagged `vX.Y.Z` once its section is complete.

## [Unreleased]

### Added
- A critical hit („Volltreffer!“) stands out in a fight page's
  „Kampfverlauf“: bold on a gold background.

### Changed
- In a boss fight's „Kampfverlauf“ only the boss's name („Relaxo“) is in its
  type colour, no longer the brackets after it („(Boss, HP 503)“).
  Older fights change with „Alles neu veröffentlichen“; until then their boss
  stays uncoloured.
- On wide screens a fight page's two cards (trainers and opponent) are wider
  (1008 px instead of 868 px); header and text keep their width.

### Fixed
- A long Pokémon or trainer name on a fight page no longer pushes the HP bar
  onto a line of its own: only the name wraps, all bars stay lined up on the
  right.

## [1.4.0] - 2026-11-10

### Added
- A fight page's „Kampfverlauf“ shows each trainer's name in their Twitch chat
  colour, and in a boss fight the boss in the colour of its type. The owner
  links above the log use the chat colour too. Colours are darkened or
  lightened where needed so they stay readable in the light and dark theme.
  Older fights get the colours with „Alles neu veröffentlichen“.

### Changed
- In a boss fight every species a trainer owns is equally likely to be sent
  in, however many of it they have — three Digda no longer push out the one
  Arkani.

### Fixed
- The numbers in a fight page's „Kampfverlauf“ no longer skip one at every
  „Runde“ heading (the first line was 2).
- The HP bars of the survivors on a fight page line up, however many digits
  their „HP left / max HP“ has.

## [1.3.0] - 2026-11-09

### Added
- The website has a „Neuigkeiten“ page that tells players in simple words what
  changed in the game, per version, newest first. It starts with the changes
  of 1.1.0 and 1.2.0.

### Changed
- The website menu wraps into a second line on narrow phones instead of
  breaking a menu item in two.

## [1.2.0] - 2026-11-08

### Added
- The fight page shows how each Pokémon ended: HP left with a small bar,
  „besiegt“, or „nicht eingesetzt“ if a team fight was over before its turn
  (older fights after „Alles neu veröffentlichen“; that can take up to a day
  to show because fight pages are cached).

## [1.1.0] - 2026-11-08

### Added
- If the bot is signed in with the channel's own Twitch account instead of the
  bot account, the app says so instead of silently ignoring the streamer.
- The OBS card says when the overlay could not start (port in use) and how to
  refresh the source if OBS was started first.
- The website says when its data is still in an older format after an update.
- Whoever joins a boss fight in its last 10 seconds for the first time extends
  the countdown by 10 seconds, so a last-second `!boss` still counts.
- When the boss escapes, the bot says how many HP it had left; the fight page
  and the desktop list of fights say it too (for older fights after „Alles neu
  veröffentlichen“).

### Changed
- The boss lobby stays open for 2 minutes instead of 60 seconds.
- Error and hint replies (unknown name, too few Pokémon, cooldown …) go out at
  most once per viewer and 30 seconds, so spam cannot delay fight results.
- The bot repeats a name typed in chat only if it is a plain name; `!challenge
  @Eich` works like `!challenge Eich`.
- The desktop list of fights shows the same headline and result as the website.
- „Alles neu veröffentlichen“ also removes files of trainers and fights the
  database no longer knows (after restoring an older backup), and new fights
  are uploaded before the rest of a full republish.
- Lead moderators can use `!entfernen` and `!bossreset`.
- Durations read the same in every reply („1 Stunde 30 Minuten“).

### Fixed
- The bot reconnects after more kinds of Twitch errors instead of staying
  offline until „Erneut versuchen“, and it no longer hangs when a connection
  attempt gets no answer.
- Shortly after going live, a lagging Twitch status no longer keeps the bot
  closed for up to an hour; it asks again a few times.
- Replies queued when the app is closed or while it reconnects are still sent.
- A fight page that cannot be loaded no longer claims the fight does not exist;
  website pages keep polling after a timeout.
- Deleting a trainer also drops their open challenge, and `!entfernen` never
  guesses between two trainers with the same name.
- A new R2 key is tried at once instead of after the previous failures' wait.
- The overlay shows „1 Art“ instead of „1 Arten“; the leaderboard's „Stand“
  shows the date on other days.

### Security
- The release workflow builds and tests with a read-only token; the log file
  keeps no names or chat text; the privacy page lists everything stored.

## [1.0.3] - 2026-11-08

### Added
- The bot tells the chat when the next boss can be called, 30 minutes after a
  boss fight („Der Boss ist wieder bereit …“).
- `!version` answers like `!pokearena`: the bot is online, with its version.

### Fixed
- The NPC table on the „So geht's“ page no longer stretches across the whole
  page.

## [1.0.2] - 2026-11-08

### Added
- The website has a „So geht's“ page for players: registration, all chat
  commands, boss, trainer and NPC fights, and which NPC unlocks at how many
  different Pokémon.

### Changed
- After `!boss`, players have 60 seconds instead of 30 to join the fight.

## [1.0.1] - 2026-11-04

### Changed
- Bosses have less HP (1.5 instead of 1.8 per participant): groups win more
  often, and each participant catches the boss in about 57–70 % of the
  fights instead of 50–64 %.

## [1.0.0] - 2026-11-04

### Added
- Moderators can delete a trainer with `!entfernen @name` (confirmed with
  `!entfernen @name ja` within 60 seconds) or in the app's trainer list. The
  trainer's Pokémon, achievements and badges are gone, their page disappears
  from the website, and old fights show „[gelöscht]“ instead of the name.
- `!bossreset` (moderators) clears the boss cooldown.
- Moderator commands work while the stream is offline.
- The app backs up the game data on every start and with „Jetzt sichern“;
  the last ten backups are kept.
- „Einstellungen“ card for the Twitch channel and the website addresses;
  they are kept across updates.
- Releases come as a ZIP on GitHub; the app shows when a newer version is
  available.

### Changed
- Fight pages on the website are cached for a day instead of a year, so a
  deleted name disappears everywhere within a day.
- Clearer chat texts: new achievements say how many species were collected
  and how to challenge the NPC, NPC cooldowns read „in 46 Stunden 30
  Minuten“ instead of „46:30“, and the welcome, registration hint and „too
  few for the boss“ messages say what to do next.

## [0.7.0] - 2026-11-03

### Added
- The bot plays only while the stream is live. Offline, game commands get
  „PokeArena läuft nur, solange der Stream live ist. Bis zum nächsten
  Stream!“ — at most once every five minutes. Fights already running finish
  normally.
- Desktop app: the Twitch card shows whether the stream is live, and the
  switch „Auch ohne Stream“ lets the streamer test offline (off again after
  every start). The dry run works as before, live or not.

## [0.6.0] - 2026-11-01

### Added
- Public website: leaderboard, list of the latest fights, a page per fight
  with both teams and the full course, and a page per trainer with Pokédex,
  achievements, badges and recent fights. Pokémon appear as names with their
  types, without pictures. The site updates itself every 15 seconds.
- Fight results in chat end with a link to the fight page („Zum Kampf: …“);
  `!arena` posts the website address. Dry-run fights get no link.
- Desktop app: the „Website“ card takes the Cloudflare key once, shows
  whether the upload works and offers „Alles neu veröffentlichen“. Uploads
  wait while the PC is offline and catch up on their own.
- OBS overlay: a browser source on the streamer's PC
  (`http://localhost:5299/overlay`) with the latest fight and the top five.

### Changed
- Leaderboard ties go to whoever completed their current set of species
  first; losing a species and winning it back counts from the comeback.

## [0.5.0] - 2026-10-27

### Added
- NPC fights: `!challenge Eich` fights an NPC the player has unlocked through
  an achievement — six random Pokémon of the player against the NPC's fixed
  team. The first win against an NPC brings its badge, tokens (Eich 1 up to
  Ash 17, adjustable in `npcs.json`; to be redeemed with the streamer later)
  and one Pokémon of its team, preferably one the player does not own yet.
  Further wins bring nothing. After a loss the same NPC can be challenged
  again after 48 hours, after a win after 60 minutes.
- Desktop app: "Kämpfe" also lists NPC fights; the trainer list shows badges
  and tokens.
- Dry run: pick an NPC and press „Freischalten“ to give the test viewer
  enough different Pokémon to challenge it.

## [0.4.0] - 2026-10-20

### Added
- Trainer fights: `!fight @Name` challenges another player, who accepts with
  `!fight @Name` within 2 minutes. Both need at least six Pokémon; each side
  fights with six random ones, and the winner takes one Pokémon from the
  loser's team — never the starter. Achievements reached this way are
  announced in chat. `@Name` also matches the Twitch login, so players with a
  non-Latin display name can be challenged too.
- Dry run: a button gives the typed test viewer six Pokémon (registering them
  if needed), so trainer fights can be tried right away.
- Desktop app: the "Kämpfe" area also lists trainer fights with winner and
  loot.

## [0.3.0] - 2026-10-19

### Added
- Boss fights: `!boss` opens a lobby for 30 seconds, other registered players
  join with `!boss`. With at least two players a boss appears — rare and
  legendary bosses get likelier the more players join — and the fight is
  decided at once. Everyone whose Pokémon is still standing catches the boss.
  Afterwards the next boss can be called after 30 minutes (2 minutes if too
  few joined).
- Achievements: reaching 15, 25, … distinct species is announced in chat
  together with the NPC it unlocks; achievements are never lost.
- Battle engine with damage formula, types, critical hits and a stored seed
  and full log for every fight.
- Desktop app: "Kämpfe" area with the latest fights and their course.
- In the dry run boss cooldowns are skipped, so a test can be repeated at once.
- `tools/BalancingSimulator.cs` simulates fights to check the balancing values.

## [0.2.0] - 2026-10-13

### Added
- Viewers register with `!registrieren <Starter>` (Bisasam, Schiggy, Glumanda,
  Pikachu or Evoli); the choice is final and survives restarts.
- Game commands for registered players point newcomers to `!registrieren`.
- Desktop app: "Trainer" area with a searchable list of all trainers.
- The dry run now uses its own test data that disappears when it ends; real
  chat commands are not carried out while it is on.

## [0.1.0] - 2026-10-13

### Added
- Game specification (`docs/spec.md`) and Gen-1 type chart (`data/typen.json`).
- Documentation structure: `CLAUDE.md`, architecture, ADRs 0001–0004,
  roadmap, backlog, work-package log.
- Base data `data/pokemon.json` with official Gen-1 values for all 151
  species, generated by `tools/GeneratePokemonData.cs` (ADR-0005).
- Desktop app skeleton: Blazor Hybrid window (light/dark following Windows),
  log files under `%LOCALAPPDATA%\PokeArena\logs`, single running instance,
  secrets in the Windows Credential Manager (ADR-0007).
- Solution, shared build settings and CI on GitHub Actions.
- README with project overview, build steps, roadmap and fan-project
  disclaimer.
- Rule against official Pokémon assets in repo, app, website and overlay
  (ADR-0008).
- Twitch connection: sign in the bot account with a code (works from the
  phone too), the bot reads and writes the channel's chat and reconnects on
  its own after connection drops.
- `!pokearena` answers that the bot is online and shows its version.
- Desktop app: connection status with plain-German hints, live chat log, and
  a dry run ("Probelauf") to try commands without writing to Twitch.
