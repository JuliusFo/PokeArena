# Trainer links on the fight page — user request (2026-11-26, branch claude/kampf-trainer-link-3fc5e0)

- Request: „Können wir dort auch den Trainer verlinken wie auf den anderen
  Seiten?“ for a fight page (an NPC fight: the trainer's name heads the card,
  the rows name no owner, so the page had no link to the trainer).
- Built: `Services/SideTrainer.Of(BattleSide)` — the trainer a card stands for:
  every fighter carries the side's name as owner and one trainer id; null for
  the group of a boss fight, the boss, NPCs and deleted trainers (no id).
  `Battle.razor` links the card heading through it, tinted like the names
  (`.side h2 .tinted` keeps weight 700 like `h1.tinted`). `DamageMeter` gives
  the „Gesamt“ bars the same link and a boss fight's trainer ranking a link per
  owner with an id (`StatisticsBars.TrainerLink`, so ids are escaped like on the
  statistics page). bUnit tests: heading links with and without colour, no
  heading link for boss group / boss / NPC / deleted trainer, damage box links
  (unit and page). CHANGELOG, „Neuigkeiten“ (under 1.10.1, proposal), spec §9.
- Deliberately not done: names in the „Kampfverlauf“ stay plain text (many
  links in every line; not asked for). The Pokémon ranking of PvP/NPC fights
  links nothing — the trainer there is the plain detail after the species,
  and „Gesamt“ above already links the side.
