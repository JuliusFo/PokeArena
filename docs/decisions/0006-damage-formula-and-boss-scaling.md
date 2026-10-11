# ADR-0006: Damage formula and boss scaling

Status: accepted · Date: 2026-10-11 · Changes spec §2, §4, §5, §13
(previously: raw `ATK/DEF`, constant `BossFaktor` 5, per-species `bossZiele`)

## Context

With the official Gen-1 values (ADR-0005) a throwaway simulation on
2026-10-11 compared formulas (all 151 × 150 pairings, 1-vs-1, both orders)
and boss setups (thousands of fights per participant count `N`, participants
and boss drawn by the spec's tier probabilities).

Formula, 1-vs-1 win rates:

| Formula | Chaneira | Simsala | Species < 15 % / > 85 % | One-hit KOs | Longest fight |
|---|---|---|---|---|---|
| raw `ATK/DEF` (old spec) | 0 % | 30 % | 17 / 12 | 19 % | 105 hits |
| damped `(ATK+50)/(DEF+50)` | 41 % | 33 % | 8 / 8 | 13 % | 39 |
| damped, best of physical/special | 86 % | 60 % | 8 / 11 | 18 % | 32 |

Boss with constant factor 5 and one target: the group wins 48 % at `N=2`,
98 % at `N=5`, 100 % from `N=8`. With one target, boss HP would have to grow
with `N²` (≈ 85× base HP and 160 log lines at `N=12`). With `⌈N/3⌉` targets
the required HP grows linearly: `1.8 × N` gives a group win rate of 73–79 %
for `N` = 3…20 (60 % at `N=2`), ≈ 60 % catch chance per participant, about
4–5 rounds and ≈ 5 log lines per participant.

User decisions on 2026-10-11: damped formula with best of physical/special;
multiple boss targets with linear HP; target difficulty ≈ 75 %.

## Decision

- Damage ratio `max((ATK_a + k)/(DEF_d + k), (SPEZ_a + k)/(SPEZ_d + k))`,
  `k = Daempfung` (default 50). Special is both special attack and special
  defense, as in Gen 1.
- Boss HP = base HP × `HPFaktor` × `BossHPProSpieler` (1.8) × `N`.
- The boss attacks `⌈N / BossZieleTeiler⌉` (3) distinct random living
  participants per round.
- The per-species field `bossZiele` is dropped; it was never set in the data.

Deliberately **not**: per-species boss tuning, separate special attack and
defense stats, difficulty scaling by boss rarity (legendary bosses are
already stronger through their stats).

## Consequences

- Chaneira becomes the tank it was in Gen 1; Psychic Pokemon are viable;
  Karpador, Raupy & co. stay weak by design.
- Boss difficulty no longer depends on how many viewers join; log length
  grows linearly with `N`.
- Fine-tuning (`Skalierung`, `k`, first-strike advantage with 18 % one-hit
  KOs) is left to the balancing simulator in WP-03, which should reproduce the
  numbers above as a sanity check.
