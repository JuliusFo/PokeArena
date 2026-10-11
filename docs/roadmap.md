# Roadmap

Planned work packages up to v1.0.0. Read the package before starting it and
agree the plan with the user. Estimates: solo, "day" = one focused person-day;
total ≈ 25–31 days. Every package ends in a state that could run on stream.

Order is deliberate. `data/pokemon.json` exists since 2026-10-11 (ADR-0005),
so WP-03 is no longer blocked by base data.

## WP-00 — Scaffold (≈ 1 day) → v0.1.0

Solution, DI/hosting, config system, credential store access, CI (build +
tests), `Directory.Build.props` with warnings as errors.
Demo: `dotnet test` green in CI.

- Open: test framework (proposal xUnit v3).
- Create `src/<Project>/CLAUDE.md` files and move the pitfalls below into them.

## WP-01 — Twitch connection (3–4 days) → v0.1.0

Device-code auth, EventSub read, Helix send, rate limiter; WPF shell with
connect button, status, live chat log.
Demo: the bot answers a test command in the channel.

- User decision (2026-10-11): the user sets up the bot account and tokens.
- Open: is `channel:bot` / `user:bot` actually required with a user token over
  WebSocket? Test, then update ADR-0002.
- Proposal (review 2026-10-11): process all game commands on one serialized
  queue (e.g. `Channel<T>`) — battles resolve instantly, so this removes race
  conditions at no cost.
- Pitfalls: EventSub keepalive timeout and `session_reconnect` handling;
  public-client refresh tokens expire after 30 days.

## WP-02 — Domain foundation (2–3 days) → v0.2.0

EF Core model + migrations, base-data loader, `!registrieren`, trainer list
in the UI.
Demo: viewers can register; data survives a restart.

## WP-03 — Battle engine + boss (4–5 days) → v0.3.0

Damage, types, crits, seed, log; boss lobby, rarity draw, cooldowns,
achievements.
Demo: `!boss` playable end-to-end.

- User decisions (2026-10-11, ADR-0005): official Gen-1 values, real dual
  types (multiplied on defence, better type on attack).
- Open: review the generated rarity list (98 common / 48 rare / 5 legendary).
- Open (review 2026-10-11):
  - Damage formula: raw `ATK/DEF` explodes for the official outliers
    (Chaneira DEF 5, Onix DEF 160, Karpador ATK 10); dampen (e.g.
    `(ATK+k)/(DEF+k)`). `spez` is in the data — use e.g. `max(atk, spez)` as
    attack so Psychic Pokemon (Simsala ATK 50, Spez 135) are not useless?
  - Boss difficulty does not scale with participant count; above ~5 players
    the boss nearly always loses. Scale boss HP or `bossZiele` with N?
  - Add a Monte Carlo balancing simulator (≈ 0.5 day) to tune the above.
  - All 151 species (incl. Mew, Ditto, starters) must be in the boss pool, or
    achievements 150/151 are unreachable.
  - Progression length: expected catches for k distinct species (uniform
    lower bound) — 15: ~16, 60: ~76, 100: ~163, 151: ~845. Intended?

## WP-04 — PvP (3–4 days) → v0.4.0

Challenge/accept dialog, timeout, random teams, loot transfer.
Demo: `!fight` playable.

- Spec gap: achievement unlocks caused by PvP loot must be announced too.

## WP-05 — NPC fights (2–3 days) → v0.5.0

NPC fights + unlock announcements; reward step stubbed until E22 is decided.
Demo: `!challenge Eich` playable.

- Open (E22): NPC reward. Proposal (review): first win only — a badge on the
  website plus one random specimen from the NPC's team, preferring species
  the trainer lacks; makes the win cooldown unnecessary.

## WP-06 — Website (4–5 days) → v0.6.0

Blazor pages (leaderboard, battle, trainer, overlay), GitHub Actions deploy of
the app, R2 bucket + CORS + cache headers, R2 publisher + outbox, local
overlay endpoint.
Demo: a battle link in chat opens a live page.

- Open: how Web shares the JSON DTOs without referencing Core (shared
  contracts project vs. duplicated records).
- Open: whose Cloudflare account hosts the bucket (user or streamer)?
- Open: define "earliest attainment" for leaderboard ties when a trainer
  loses and regains a species.

## WP-07 — Operations (2–3 days) → v1.0.0

Mod commands, deletion flow, backup, republish-all, README, message wording
pass.
Demo: stream-ready.

- Open: installation and updates on the streamer's PC (self-contained
  publish, installer?).
- Pitfall: copying the SQLite file while open loses WAL content — back up with
  `VACUUM INTO` or the SQLite backup API.

## Open UX questions

- Exact German wording of all chat messages (spec §14) — drafted during
  implementation, reviewed in WP-07.
- First start for the streamer: is a guided setup (Twitch login, R2 token)
  needed, or does the user configure the PC once?
