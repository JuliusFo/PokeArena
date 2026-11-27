# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). Each release is
tagged `vX.Y.Z` once its section is complete.

## [Unreleased]

## [1.11.0] - 2026-11-27

### Added
- „Verlauf“ in the Pokédex box, after user feedback: below the base values
  one row per Pokémon of that species with how the trainer got it and when
  („Starter“, „Als Boss gefangen“, „Belohnung von Eich“, „Von Ben gewonnen“
  with a link to Ben's page) and when the species was last seen in one of
  their fights; a species only seen shows its first and latest fight. Every
  date links its fight. The bot now keeps the first and the latest fight per
  seen species (the update fills both once from the stored fight logs) and
  writes how each specimen was got into the trainer file. Until the updated
  bot has rewritten a trainer page, its box has no „Verlauf“.

### Changed
- The Pokédex on the trainer page remembers „Alle / Nur gefangene“ per viewer
  in the browser (`localStorage`, like the fight log's view), for every
  trainer page; blocked storage starts with „Alle“ as before.
- The fight log shows a hit's full damage again, in both views, after viewer
  feedback: a knockout of 210 on 40 HP left reads „−210“ next to the bar „40 →
  0“ and „und verursacht 210 Schaden“ in „Text“ (since 1.8.2 it showed only
  what arrived, „40“, which read like a hit of exactly the HP left). The HP
  arrow still shows what arrived; the „Schaden“ box keeps counting only that.
  Works for every fight file since 1.8.2 — they carry the full damage already.
- The fight page links trainers to their page like the leaderboard and the
  statistics do: the trainer's name heading a fight card (in the chat colour)
  and the trainer names in the „Schaden“ box („Gesamt“ in PvP and NPC fights,
  the ranking in a boss fight). The group of a boss fight, the boss, NPCs and
  deleted trainers stay plain text.
- HP on the fight page change colour gradually instead of in three steps:
  red up to a quarter, turning to orange at half and to green at three
  quarters, green above. The bars on the fight cards and in the log and the
  HP numbers in both log views share it; every shade stays readable with the
  white numbers and on the light and dark page.

### Fixed
- The Pokédex search: closing it with Escape after opening it with the mouse
  no longer draws a frame around „Name“ (Escape moves the focus back there
  only when it was opened with the keyboard; „×“ always does). A tap or
  click on free space — anywhere that is not a row, a button or a link — now
  closes an empty search too; with a name typed in it stays open, so tapping
  beside it to hide a phone's keyboard keeps the results.
- An empty desktop window with two boxes „Ein unerwarteter Fehler ist
  aufgetreten: 0x8876086A“ after the app restarted while nobody was connected
  to the server (likely the automatic update): the window content needs a
  graphics device, which a disconnected remote desktop session does not have.
  The app now opens its window only once someone connects (the bot runs
  before that). If the content still fails to load, it shows no box and tries
  again after 2, 5 and 15 seconds; if that fails too, the window says „Die
  Anzeige konnte nicht geladen werden. Der Bot läuft trotzdem weiter. Bitte neu
  verbinden oder PokeArena neu starten.“ and loads again at the next
  connection.

## [1.10.0] - 2026-11-23

### Added
- The trainer page's Pokédex, after a viewer's idea: all 151 species in a
  scrolling list next to a box with the selected one (types, „Starter“, count,
  base values HP / ATK / DEF / SPEZ). Pokémon the trainer has met in their
  fights but does not own stand greyed with „gesehen“, all others as
  „????????“. A switch „Alle / Nur gefangene“, and a click on „Name“ opens a
  search by name or number. On a phone the box opens below the selected row.
- The bot remembers which species each trainer has seen (new table
  `SeenSpecies`, written with every fight; the update fills it once from the
  stored fight logs) and uploads a new site file `arten.json` with every
  species' types and base values. Until the updated bot has uploaded it, the
  Pokédex shows the caught species without base values.

### Changed
- The fight log's „Balken“ view colours HP like „Text“, after viewer feedback:
  the attacker's HP in the sentence are green, orange or red by what is left,
  and the HP bars (log and fight cards) get the orange step at half or less
  besides red at a quarter or less. A hit the page cannot draw keeps its
  sentence as written.

## [1.9.0] - 2026-11-22

### Added
- A switch „Balken / Text“ over the fight page's „Kampfverlauf“, after viewer
  feedback that not everyone likes the hit bars. „Text“ shows every hit as one
  sentence again, with coloured numbers: „Glumanda (Clara, HP 78) trifft Relaxo
  (Boss, HP 541 → 503) und verursacht 38 Schaden.“ — HP green, orange or red by
  what is left, the damage (what arrived) in bold red. Each viewer's browser
  remembers the pick; the bars stay the default. Log parts mark the attacker's
  HP (`isHp`) and the damage (`isDamage`); desktop lists and chat are
  unchanged. Older fights get the new wording once the bot has uploaded the
  website again after its update.
- A website page „Einstellungen“ (`/einstellungen`, gear icon at the end of the
  menu): the look „Automatisch“ (default, follows the device), „Hell“ or
  „Dunkel“, and the fight log's „Balken / Text“. Both picks stay in the
  viewer's browser. The dark colours now hang on `data-theme` on `<html>`, set
  by a small script in `index.html` before the first paint, instead of on
  `prefers-color-scheme`.

## [1.8.2] - 2026-11-20

Fixes of the whole review 1.8.1 (R2, `docs/review-1.8.1.md`).

### Changed
- The fight page's „Kampfverlauf“ shows each hit's damage („−38“) right
  before its HP bar instead of in the sentence (the damage that arrived, so
  a knockout matches the bar and the „Schaden“ box), after viewer feedback that
  reading the damage on the left and the HP on the right was tiring. The
  sentence gets shorter: „Glumanda (Clara, HP 78) trifft Relaxo (Boss).
  Volltreffer!“. Desktop lists and chat are unchanged. Older fights get the
  new look once the bot has uploaded the website again after its update.
- After an update the bot uploads every page of the website once by itself, so
  older fights and trainer pages pick up the new look without anyone clicking
  „Alles neu veröffentlichen“ (the button still also cleans up the bucket).
- A boss that beat every Pokémon reads „… ist entkommen – alle Pokémon wurden
  besiegt.“ on the fight page and in the desktop app too, like in chat.
- The privacy page says the data lies on the machine the bot runs on.
- „Schlüssel entfernen“ in the website card asks first.
- The data host's Cloudflare rule lets only the site's own files through and an
  R2 billing alert is required (`docs/website-setup.md` §2; to set by hand).

### Fixed
- The bot starts even if the overlay cannot open its port for another reason
  than „in use“ (for example a port Windows reserved); only the overlay is off.
- A slow or busy Twitch answer no longer ends a working connection or a login
  in progress.
- A release deleted after its download is no longer installed.
- Errors at the very start of the app reach the log file.
- A failed „Alles neu veröffentlichen“ shows a hint in the card instead of an
  error box.
- A colour change in chat and new profile pictures cannot lose a viewer's
  command or the pictures when the website upload runs at the same moment.
- Commands work with any kind of space after the command name.
- Broken base data files stop the app with a readable message; a changed
  achievement threshold is refused.

## [1.8.1] - 2026-11-19

### Changed
- The fight page's „Kampfverlauf“ shows the hit Pokémon's HP as a small bar
  to the right of every hit: filled up to the HP after the hit, the piece the
  hit took hatched in red, the numbers („80 → 22“) inside. The sentence no
  longer repeats them, so the lines are shorter; the attacker keeps its HP in
  the sentence. Desktop lists and chat are unchanged. After updating, the
  streamer clicks „Alles neu veröffentlichen“ once so older fights get the
  bars; until then they show the old sentences.

## [1.8.0] - 2026-11-19

### Added
- The leaderboard shows a small trophy before ranks 1 to 3 — gold, silver
  and bronze. Drawn by the site itself (no emoji, no official asset),
  hidden from screen readers.

### Changed
- Boss fights are easier and no longer depend on the group size: the boss
  attacks a third of the group per round on average (the remainder carried
  into the next rounds, e.g. 2, 1, 1 with four trainers) instead of
  ⌈N / 3⌉ every round, and boss HP per participant drops from 1.5 to 1.3.
  On stream 182 boss fights up to 2026-11-18 won only 50.5 %, groups of four
  34 %; the simulator's new campaign mode (trainers own only what they
  caught) expects ≈ 75 % for every group size instead of 57 %.
- The first win against an NPC gives 1 token for every NPC instead of a
  staggered 1 (Eich) … 17 (Ash), on the streamer's request. Balances already
  earned are not changed.
- Going back in the browser returns to the same spot of the website: after
  scrolling down the list of fights, opening one and going back, the list is
  where it was instead of at the top. The same holds for the leaderboard,
  trainer pages and the statistics, for forward and for reloading a page.

### Fixed
- The fight log shows a hit's target with its HP before and after the hit
  („Digda (Ash, HP 10 → 0)“) instead of only the HP after it, so a
  knockout no longer reads as a hit on a Pokémon with 0 HP. The text is
  rendered from the stored fight, so after updating the streamer clicks
  „Alles neu veröffentlichen“ once to fix older fight pages too.

## [1.7.1] - 2026-11-11

### Added
- The leaderboard shows each trainer's Twitch profile picture small before the
  name, and every name in the trainer's Twitch chat colour. The trainer page
  shows the name in that colour too. After updating, the streamer clicks
  „Alles neu veröffentlichen“ once, so that every trainer page gets its colour;
  later a new picture or a new chat colour updates both pages by itself.
  The fight lists keep plain names.

## [1.7.0] - 2026-11-11

### Added
- A website page „Statistik“ with rankings beyond the leaderboard, as bars
  like the „Schaden“ box:
  - the community's Pokédex (species owned by anyone, out of 151) with
    totals for trainers, fights and boss catches
  - most boss fights taken part in, and most PvP fights with wins
  - „Unglücksraben“: the longest run of boss fights without a catch
  - „Erzrivalen“: the pairs with the most PvP fights against each other
  - species only one trainer owns
  - the bosses that appeared and escaped most often
- The page updates itself after every fight. After updating, the streamer
  clicks „Alles neu veröffentlichen“ once, so that the page is there before
  the next fight.
- Trainer pages show the viewer's Twitch profile picture left of the name.
  The bot fetches the pictures itself (new trainers within 15 minutes, every
  picture checked once a day) and uploads a copy with the other website data,
  so visitors' browsers never load anything from Twitch. A new picture can
  take up to two days to show (daily check plus one day of caching). Deleting
  a trainer deletes the picture too. After updating, nothing needs to be
  clicked: the pictures arrive by themselves while the bot is connected to
  Twitch.

### Fixed
- After a release a browser could keep the old stylesheet from its cache next
  to the new website code; after 1.6.0 Firefox showed the HP numbers inside the
  HP bar twice, one above the other. The website deploy now gives the
  stylesheet a new address whenever its content changes.

## [1.6.2] - 2026-11-10

### Changed
- The white text inside the „Schaden“ bars on a fight page has a thin black
  outline, so it stays clear on lighter colours such as pink or red.

## [1.6.1] - 2026-11-10

### Changed
- The text inside the „Schaden“ bars on a fight page is white like the HP
  numbers instead of black or white depending on the colour. The bars take the
  darkened colour of the trainer's name, so light chat colours get darker bars
  (yellow turns olive).
- Names in the fight cards and the log keep that darkened colour in the dark
  theme too, so a name and its bar always match. Dark chat colours can be
  harder to read on the dark page than before.

## [1.6.0] - 2026-11-10

### Added
- A fight page shows a „Schaden“ box between the fight cards and the log: what
  each side dealt in total, then a ranking of who dealt how much — the
  trainers in a boss fight (with their share of the boss's HP), every Pokémon
  sent in for PvP and NPC fights. Bars run in the trainer's Twitch chat colour
  (the boss and NPC Pokémon in their type's colour) with name and number
  inside. Only damage that arrived counts. Fight files carry the damage per
  fighter as a new optional field; older fights show the box after „Alles neu
  veröffentlichen“.

### Fixed
- The HP numbers inside a fight page's HP bar look crisp again instead of thin
  and grey: they are drawn as two stacked texts (white over the fill, dark
  over the rest) instead of one text with a colour gradient, which browsers
  render without subpixel smoothing.

## [1.5.0] - 2026-11-10

### Added
- `!bosscooldown <Minuten> [Cooldown]` (moderators) shortens the boss cooldown
  for up to 24 hours: `!bosscooldown 60` halves it (15 instead of 30 minutes)
  for an hour, `!bosscooldown 60 10` sets it to 10 minutes for an hour,
  `!bosscooldown aus` ends it early. A cooldown already running shrinks by the
  same factor, and the bot still announces when the boss is ready. The
  shortened cooldown survives an app restart; `BossCooldownMinutes` itself does
  not change.
- The desktop app updates itself: it looks for a new version five minutes
  after the start and then hourly, downloads it and installs it once the
  stream has been over for ten minutes and no boss lobby is open, then
  restarts by itself. The top
  bar shows what is going on, „Jetzt aktualisieren“ installs at once and
  „Nach Updates suchen“ looks right away. Releases now come as `Setup.exe` and
  `Portable.zip` instead of the ZIP; switching needs one install with
  `Setup.exe` as the same Windows user (README „Installing and running“).
  Game data, settings and sign-ins stay where they are.

### Changed
- On a fight page a surviving Pokémon's HP numbers („48 / 90 HP“) sit inside
  its HP bar instead of next to it, so the end state column is narrower.
  In the dark theme the bar uses the same dark green and red with white
  numbers as in the light theme; the bright green was hard to read.

## [1.4.2] - 2026-11-10

### Changed
- A fight page's two cards (trainers and opponent) are as wide as the rest of
  the page again (868 px); the wider cards of 1.4.1 looked lopsided. Long names
  still wrap without pushing the HP bar onto a line of its own.

## [1.4.1] - 2026-11-10

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
