# Backlog

Unplanned ideas. Never implemented on the side — move into a work package in
roadmap.md first. Format: idea — date, origin.

## Continuation of WP-01 (chat)

- Throttle repeated cooldown/error replies per user so chat is not spammed —
  2026-10-11, design review.
- `!hilfe` command listing the commands — 2026-10-11, design review.
- `!dex` command: own distinct/total count in chat for viewers who don't open
  the website — 2026-10-11, design review.

## Continuation of WP-03 (boss, progression)

- Catch bonus for species the trainer does not own yet, to shorten the grind —
  2026-10-11, design review.

## Continuation of WP-04 (PvP)

- `!fight nein` to decline a challenge; today anyone can block a trainer for
  the timeout by challenging them — 2026-10-11, design review.
- Draw teams without duplicate species, so hoarding weak duplicates does not
  dilute a team — 2026-10-11, design review.
- Challenge mode from the PDF: challenger shows their team, opponent picks a
  team, winner picks the reward — 2026-09-03, spec §14.
- Cooldown between the same trainer pair if trading-by-losing is abused —
  2026-09-03, spec §6.

## Continuation of WP-05 (NPC, tokens)

- Redeeming tokens with the streamer: chat command plus a desktop screen to
  confirm and book redemptions — 2026-10-21, user decision E22.
- Token ledger (who earned or spent how many, when, why) instead of the plain
  balance, once tokens can be spent or corrected — 2026-10-21, WP-05 plan.
- Chat command for the own token balance and badges (or fold it into `!dex`)
  — 2026-10-21, WP-05 plan.

## Battle system

- Attacks per Pokemon with per-attack types — 2026-09-03, spec §14.
- Pokemon defeated in a fight become unavailable for further fights —
  2026-09-03, spec §14.
- Battle log retention/cleanup policy — 2026-09-03, spec §14.

## Continuation of WP-06 (website)

- Custom domain for the R2 bucket (needs a domain managed by Cloudflare,
  ~10 €/year) if `r2.dev` limits bite or CDN caching is wanted — 2026-10-11,
  user decision (deferred).

## Abuse protection

- Alt-account protection, e.g. minimum Twitch account age via Helix `Get
  Users` → `created_at` at `!registrieren` — 2026-10-11, user deferred it
  (small channel).
