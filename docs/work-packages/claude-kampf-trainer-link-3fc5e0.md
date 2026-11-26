# Trainer links on the fight page — user request (2026-11-26, branch claude/kampf-trainer-link-3fc5e0)

- Request: „Können wir dort auch den Trainer verlinken wie auf den anderen
  Seiten?“ for a fight page (an NPC fight: the trainer's name heads the card,
  the rows name no owner, so the page had no link to the trainer).
- Built: `Services/TrainerLinks` — `Href(id)` (relative, escaped, null for an
  empty id) for every trainer link of the site (leaderboard, statistics, fight
  page), and `OfSide(battle, side)`, the trainer a card stands for: every
  fighter carries the side's name as owner and one trainer id; null in a boss
  fight (group „Trainer“, boss), for NPCs and deleted trainers (no id).
  `Battle.razor` links the card heading through it, tinted like the names
  (`.side h2 .tinted` keeps weight 700 like `h1.tinted`); the rows link their
  owner whenever the heading does not. `DamageMeter` gives the „Gesamt“ bars
  the same link and a boss fight's trainer ranking a link per owner with an id.
  bUnit and unit tests: heading links with and without colour, no heading link
  for boss group / boss / NPC / deleted trainer, damage box links, two ids on
  one side, escaping. CHANGELOG, „Neuigkeiten“ (under 1.10.1, proposal), spec
  §9 footnote ³, Web `CLAUDE.md`.
- Code review (2026-11-26): „Fix all“. Second pass on the fixes: all five
  covered; N1 stale log (this entry) fixed, N2 escaping tests for the
  leaderboard and the statistics' single owners added (user: „Add N2“).
  - L1 fixed: a viewer named „Trainer“ alone in a boss fight made the group
    heading and its „Gesamt“ bar a link (side 0 of a boss fight is always
    „Trainer“) and lost the row link. `OfSide` returns null in boss fights;
    rows link when the heading does not. Test with such a viewer.
  - L2 fixed: the fight rows and the leaderboard built `trainer/<id>`
    unescaped and the fight page borrowed `StatisticsBars.TrainerLink`. All
    links go through `TrainerLinks.Href` now; `StatisticsBars.TrainerLink` and
    `SideTrainer` are gone.
  - L3 fixed: tests for two ids on one side, another owner, escaping, empty id.
  - L4 fixed: the spec paragraph moved into footnote ³.
  - L5 fixed: Web `CLAUDE.md` re-wrapped; the `TrainerLinks` rule is its own
    pitfall.
  - Pre-existing, not changed: a foreign fight file with `"fighters": null`
    breaks the page; a fight file cached for a day keeps a deleted trainer's
    link until it expires.
- Deliberately not done: names in the „Kampfverlauf“ stay plain text (many
  links in every line; not asked for). The Pokémon ranking of PvP/NPC fights
  links nothing — the trainer there is the plain detail after the species,
  and „Gesamt“ above already links the side.
