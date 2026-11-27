# Work packages

Overview with one log file per finished package in [work-packages/](work-packages/) (split at
74 KB, review 1.8.1 D3). Plans and open questions live in [roadmap.md](roadmap.md). "Areas" says where the
code of a package lives: read the logs of earlier packages in the same area before starting one.

| WP | Name (log) | Version | Done | Areas |
|---|---|---|---|---|
| WP-00 | [Scaffold](work-packages/wp-00-scaffold.md) | 0.1.0 | 2026-10-11 | solution, CI, Desktop host, UI shell |
| WP-01 | [Twitch connection](work-packages/wp-01-twitch-connection.md) | 0.1.0 | 2026-10-13 | Core/Twitch, Core/Chat, Secrets, UI Twitch card |
| WP-02 | [Domain foundation](work-packages/wp-02-domain-foundation.md) | 0.2.0 | 2026-10-13 | Core/Data (EF, migrations), Core/Game trainers, dry run |
| WP-03 | [Battle engine + boss](work-packages/wp-03-battle-engine-boss.md) | 0.3.0 | 2026-10-19 | Core/Game/Battles, BossEncounter, data/ |
| WP-04 | [PvP](work-packages/wp-04-pvp.md) | 0.4.0 | 2026-10-20 | PvpService, loot |
| WP-05 | [NPC fights](work-packages/wp-05-npc-fights.md) | 0.5.0 | 2026-10-27 | NpcService, npcs.json, tokens, badges |
| WP-06 | [Website](work-packages/wp-06-website.md) | 0.6.0 | 2026-11-01 | Contracts, Core/Publishing, Web, overlay |
| WP-07 | [Live status](work-packages/wp-07-live-status.md) | 0.7.0 | 2026-11-03 | TwitchConnection live state, CommandProcessor gate |
| WP-08 | [Operations](work-packages/wp-08-operations.md) | 1.0.0 | 2026-11-04 | mod commands, deletion, backup, settings, README |
| R1 | [Review 1.0.3 fixes](work-packages/wp-r1-review-1.0.3.md) ([review-1.0.3.md](review-1.0.3.md)) | 1.1.0 | 2026-11-08 | whole solution |
| – | [Boss lobby (streamer requests)](work-packages/feat-boss-lobby.md) | 1.1.0 | 2026-11-08 | BossEncounter lobby, chat texts |
| – | [Battle end state (streamer request)](work-packages/feat-battle-end-state.md) | 1.2.0 | 2026-11-08 | SiteGenerator, Web fight page |
| WP-10 | [„Neuigkeiten“ page on the website](work-packages/wp-10-news-page.md) | 1.3.0 | 2026-11-09 | Web News page |
| – | [Boss pick per species (user request)](work-packages/feat-boss-pick-per-species.md) | 1.4.0 | 2026-11-10 | BossDraw |
| – | [Battle log numbering (user report)](work-packages/fix-battle-log-numbering.md) | 1.4.0 | 2026-11-10 | BattleLog text |
| – | [HP bar alignment (user request)](work-packages/feat-hp-bar-alignment.md) | 1.4.0 | 2026-11-10 | Web fight page CSS |
| – | [Fight log colours (user request)](work-packages/feat-fight-log-colours.md) | 1.4.0 | 2026-11-10 | Contracts LogPart, Web Tint |
| – | [Fight log: boss name only, critical hits (user request)](work-packages/feat-fight-log-boss-name-crit.md) | 1.4.1 | 2026-11-10 | BattleLog spans, Web log |
| – | [Fighter row wrap (user report)](work-packages/fix-fighter-row-wrap.md) | 1.4.1 | 2026-11-10 | Web fight cards CSS |
| – | [Fight card width reverted (user report)](work-packages/fix-fight-card-width.md) | 1.4.2 | 2026-11-10 | Web fight cards CSS |
| – | [Website deployed with the release tag (user request)](work-packages/feat-pages-on-release-tag.md) | – (workflow) | 2026-11-10 | pages.yml, release skill |
| – | [HP numbers inside the bar (user request)](work-packages/feat-hp-text-in-bar.md) | 1.5.0 | 2026-11-10 | Web .hp-bar |
| – | [Shorter boss cooldown for a while (streamer request)](work-packages/feat-boss-cooldown-reduced.md) | 1.5.0 | 2026-11-10 | BossEncounter cooldowns, !bosscooldown |
| WP-11 | [Automatic update of the desktop app](work-packages/wp-11-auto-update.md) | 1.5.0 | 2026-11-10 | UpdateCheck, Velopack, release.yml |
| – | [Crisp HP numbers (user report)](work-packages/fix-hp-text-crisp.md) | 1.6.0 | 2026-11-10 | Web .hp-bar |
| – | [„Schaden“ box on the fight page (user request)](work-packages/feat-damage-meter.md) | 1.6.0 | 2026-11-10 | Contracts Damage, Web DamageBox |
| – | [White text in the „Schaden“ bars (user request)](work-packages/feat-damage-text-white.md) | 1.6.1 | 2026-11-10 | Web Tint, bars |
| – | [Black outline for the „Schaden“ bar text (user request)](work-packages/feat-damage-text-outline.md) | 1.6.2 | 2026-11-10 | Web bars CSS |
| – | [Stylesheet cache busting (user report)](work-packages/fix-css-cache-busting.md) | 1.7.0 | 2026-11-10 | pages.yml |
| – | [„Statistik“ page on the website (user idea)](work-packages/feat-statistics-page.md) | 1.7.0 | 2026-11-11 | SiteGenerator statistics, Web Stats, BarList |
| – | [Profile picture on the trainer page (user idea)](work-packages/feat-trainer-profile-image.md) | 1.7.0 | 2026-11-11 | ProfileImageRefresher, SitePaths, Web trainer page |
| – | [Leaderboard pictures and chat colours (user request)](work-packages/feat-leaderboard-profile-colours.md) | 1.7.1 | 2026-11-11 | Web Home, TrainerService colour |
| – | [Fight log: HP before the hit (viewer feedback)](work-packages/fix-battle-log-hp-before.md) | 1.8.0 | 2026-11-18 | BattleLog text |
| – | [Flaky EventSub keepalive test (code review finding)](work-packages/fix-flaky-keepalive-test.md) | – (tests only) | 2026-11-19 | Core.Tests timers |
| – | [„Zurück“ keeps the scroll position (streamer feedback)](work-packages/feat-back-keeps-scroll.md) | 1.8.0 | 2026-11-19 | Web index.html script |
| – | [Boss targets and HP from stream data (user request)](work-packages/feat-boss-balancing.md) | 1.8.0 | 2026-11-19 | BossBattle targets, BossSettings, simulator |
| – | [Leaderboard trophies (user request)](work-packages/claude-ranking-trophy-icons-458526.md) | 1.8.0 | 2026-11-19 | Web Home, Trophy |
| – | [One token per NPC (streamer feedback)](work-packages/claude-npc-token-scaling-4763c0.md) | 1.8.0 | 2026-11-19 | npcs.json, guide page |
| – | [HP bar per hit in the fight log (user pick)](work-packages/claude-hp-balken-kampf-log-mockup-de3cfe.md) | 1.8.1 | 2026-11-19 | BattleLog hits, Contracts LogHit, Web .hit-bar |
| R2 | [Review 1.8.1 fixes](work-packages/wp-r2-review-1.8.1.md) ([review-1.8.1.md](review-1.8.1.md)) | 1.8.2 | 2026-11-19 | whole solution |
| – | [Damage next to the hit bar (viewer feedback)](work-packages/claude-kampflog-hp-balken-feedback-808390.md) | 1.8.2 | 2026-11-20 | BattleLog spans, Contracts LogHit.Damage, Web .hit-numbers |
| – | [Text view for the fight log (viewer feedback)](work-packages/claude-kampflog-design-options-cb7042.md) | 1.9.0 | 2026-11-21 | BattleLog spans, Contracts LogPart.IsHp/IsDamage, Web log view switch |
| – | [Settings page on the website (user request)](work-packages/claude-settings-page-design-00654c.md) | 1.9.0 | 2026-11-21 | Web Settings page, ViewerSettings, index.html theme script, site.css data-theme |
| – | [Coloured HP in the bar view (viewer feedback)](work-packages/claude-hp-coloring-bar-view-4f3c63.md) | 1.10.0 | 2026-11-23 | Web Battle.razor HP steps, site.css .hp-bar.mid |
| – | [Pokédex on the trainer page (viewer idea)](work-packages/claude-pokedex-trainer-page-improve-a4a504.md) | 1.10.0 | 2026-11-23 | Contracts SpeciesList/TrainerPage.Seen, Core SeenSpecies + Sightings + arten.json, Web Shared/Pokedex |
| – | [Empty window after a restart without a session (user report)](work-packages/fix-wpf-software-rendering.md) | 1.10.1 (proposal) | 2026-11-23 | Desktop App start, InteractiveSession, MainWindow |
| – | [Pokédex switch remembered (user request)](work-packages/claude-pokedex-toggle-persistence-c87bd9.md) | 1.10.1 (proposal) | 2026-11-23 | Web ViewerSettings, Shared/Pokedex |
| – | [Pokédex search: focus frame, click outside (user feedback)](work-packages/claude-pokedex-search-ui-fixes-79cb0c.md) | 1.10.1 (proposal) | 2026-11-24 | Web Shared/Pokedex, index.html |
| – | [Trainer links on the fight page (user request)](work-packages/claude-kampf-trainer-link-3fc5e0.md) | 1.10.1 (proposal) | 2026-11-26 | Web Battle.razor, TrainerLinks, DamageMeter, Home, Stats |
| – | [Full hit damage in the fight log again (viewer feedback)](work-packages/claude-damage-display-capping-25b0a0.md) | 1.10.1 (proposal) | 2026-11-27 | Web Pages/Battle |
| – | [Gradual HP colours (user request)](work-packages/claude-hp-bar-color-gradient-ee5d5b.md) | 1.10.1 (proposal) | 2026-11-27 | Web Services/HpColor, Battle.razor, site.css |
| – | [Pokédex box „Verlauf“ (user feedback)](work-packages/claude-pokedex-details-layout-76a090.md) | 1.10.1 (proposal) | 2026-11-27 | Core SeenSpecies dates + Sightings + SiteGenerator, Contracts SpecimenInfo/SightingInfo, Web Shared/Pokedex |
| WP-09 | Balancing after the first streams | 1.11.0 (proposal) | planned | BossSettings, PvP loot, NPC strength |

## Adding a package

A row above and a file `work-packages/<branch with - instead of />.md`:

```
# WP-NN — Name (YYYY-MM-DD, branch wp/NN-short-name)

- Built:
- User decisions:
- Addenda:
- Pitfalls:
- Deliberately not done (→ backlog):
```
