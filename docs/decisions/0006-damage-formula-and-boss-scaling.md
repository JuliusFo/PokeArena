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
  `k = Damping` (default 50). Special is both special attack and special
  defense, as in Gen 1.
- Boss HP = base HP × `HpFactor` × `BossHpPerParticipant` (1.8; 1.5 since the
  addendum 2026-11-04; 1.3 since the addendum 2026-11-19, planned for 1.8.0)
  × `N`.
- The boss attacks `⌈N / BossTargetDivisor⌉` (3) distinct random living
  participants per round — `N / BossTargetDivisor` on average, the remainder
  carried over, since the addendum 2026-11-19.
- The per-species field `bossZiele` is dropped; it was never set in the data.

Deliberately **not**: per-species boss tuning, separate special attack and
defense stats, difficulty scaling by boss rarity (legendary bosses are
already stronger through their stats).

## Consequences

- Chaneira becomes the tank it was in Gen 1; Psychic Pokemon are viable;
  Karpador, Raupy & co. stay weak by design.
- Boss difficulty no longer depends on how many viewers join; log length
  grows linearly with `N`.
- Fine-tuning (`Scaling`, `k`, first-strike advantage with 18 % one-hit
  KOs) is left to the balancing simulator in WP-03, which should reproduce the
  numbers above as a sanity check.

## Addendum 2026-10-15 (WP-03)

`tools/BalancingSimulator.cs` runs the real engine (fixed seeds; 151 × 150
ordered pairings × 10, 5 000 boss encounters per `N`). With the defaults it
reproduces the table above:

| `Scaling` | Chaneira | Simsala | < 15 % / > 85 % | One-hit KOs | First strike wins | Hits avg / max | Boss group wins N=2 / 3 / 4 / 5 / 10 / 20 | Log lines per participant |
|---|---|---|---|---|---|---|---|---|
| 25 (old default) | 87 % | 60 % | 9 / 11 | 20.4 % | 58.9 % | 3.1 / 20 | 62 / 77 / 66 / 73 / 75 / 75 % | 5.2–6.7 |
| 20 (default since 2026-10-19) | 87 % | 63 % | 10 / 11 | 13.2 % | 57.5 % | 3.8 / 24 | 60 / 76 / 64 / 71 / 74 / 75 % | 6.4–8.1 |
| 15 | 88 % | 61 % | 9 / 11 | 7.6 % | 55.6 % | 4.9 / 32 | 58 / 74 / 62 / 70 / 71 / 74 % | 8.3–10.2 |

- "One-hit KOs" = 1-vs-1 fights decided by the first hit. Lower `Scaling`
  mainly cuts them and the first-strike advantage; type and stat balance stay
  the same; boss win rates drop slightly and logs grow.
- `N = 4` is a dip (≈ 65 %): `⌈4/3⌉ = 2` boss targets hit half the group per
  round.
- Catch chance per participant ≈ 52–65 %, about 4–5 rounds per boss fight; the
  round limit was never reached.
- The defaults stayed unchanged until the user decided on fine-tuning (see the
  addendum 2026-10-19).

## Addendum 2026-10-19 (WP-03)

User decision: `Scaling` default **20** (was 25), based on the simulator
table of the 2026-10-15 addendum — one-hit KOs drop from 20 % to 13 %, the
first-strike advantage from 59 % to 57.5 %; boss group win rates fall by only
1–2 points, logs grow by about one line per participant. `k` stays 50. The
`N = 4` dip (≈ 64 %) is still open (roadmap.md).

## Addendum 2026-11-04 (after 1.0.0)

User decision: `BossHpPerParticipant` default **1.5** (was 1.8), to raise the
catch chance. Simulator (`dotnet run tools/BalancingSimulator.cs -- --duels 1
--bosses 3000`, fixed seeds), catch chance per participant / group wins:

| Variant | N=2 | 3 | 4 | 5 | 6 | 8 | 10 | 15 | 20 | Group wins |
|---|---|---|---|---|---|---|---|---|---|---|
| HP 1.8, divisor 3 (old) | 51 % | 64 % | 50 % | 57 % | 63 % | 59 % | 57 % | 62 % | 59 % | 59–78 % |
| **HP 1.5, divisor 3 (new)** | 57 % | 70 % | 57 % | 64 % | 70 % | 66 % | 65 % | 69 % | 66 % | 66–84 % |
| HP 1.3, divisor 3 | 63 % | 75 % | 63 % | 70 % | 75 % | 72 % | 70 % | 74 % | 72 % | 71–88 % |
| HP 1.8, divisor 4 | 51 % | 64 % | 72 % | 57 % | 63 % | 72 % | 67 % | 69 % | 70 % | 59–86 % |
| HP 1.8, divisor 5 | 51 % | 64 % | 72 % | 78 % | 63 % | 72 % | 79 % | 78 % | 77 % | 59–92 % |
| HP 1.5, divisor 4 | 57 % | 70 % | 78 % | 64 % | 70 % | 78 % | 73 % | 76 % | 77 % | 66–92 % |

- Lower boss HP lifts every group size by about the same 6–7 points and
  shortens fights by about half a round; the group win rate target moves from
  ≈ 75 % to ≈ 80 %. About 13 % fewer bosses are needed for the same
  collection.
- A larger divisor was rejected: `⌈N / divisor⌉` makes the effect jump between
  neighbouring group sizes (divisor 4: N=4 72 %, N=5 57 %), which looks random
  to players.
- Changing the catch rule (everyone catches on a win, or a catch roll for
  fainted participants) was not chosen: survival as the catch condition
  (spec §4) stays.
- The `N = 4` dip remains (57 % vs. 70 / 64 % around it) and stays a WP-09
  topic. If 1.5 is still too stingy after a few streams, 1.3 is the next step.

## Addendum 2026-11-19 (WP-09, stream data)

The 182 boss fights on stream up to 2026-11-18 (fights 1–203, from the
published fight files) won **50.5 %** and caught **37.7 %** per participant —
far below the simulator's ≈ 80 % / 57–70 %. By lobby size: N = 2 / 3 / 4 / 5 /
6 / 7 / 8 won 64 / 55 / **34** / 56 / 65 / 47 / 60 % (14 / 38 / 53 / 34 / 23 /
15 / 5 fights). By the boss's stat sum (HP + Atk + Def + Spez): below 250
82 %, 250–329 36 %, 330–399 10 %, from 400 11 %; legendaries 0 of 4.

Cause: parts 2 and 3 of the simulator let participants field species drawn
like bosses. A trainer only owns the bosses their group beat — mostly the weak
ones — so collections stay weaker than the bosses: fielded stat sum 202 on
average against bosses of 265 (194 in the first, 211 in the second half of the
fights).

Part 4 of the simulator (`dotnet run tools/BalancingSimulator.cs -- --part 4`)
replays whole campaigns: 12 trainers start with a random starter and add the
boss whenever they survive a won fight; lobby sizes follow the stream fights;
PvP transfers and NPC rewards are left out. Who joins is a model of a small
channel, not the stream's per-viewer counts (no player data in the
repository, ADR-0003): every trainer joins 0.85 × as often as the one before,
a round decay not fitted to anyone, scaled to the mean lobby size and
calibrated, because drawing a lobby one by one by weight evens the rates out
(review feat/boss-balancing). With the target rule before 1.8.0 (`⌈N/3⌉`,
below) and HP 1.5, divisor 3; 500 campaigns × 500 fights:

| Fights | Group wins | Catch/participant | Stat sum fielded / boss | Species owned |
|---|---|---|---|---|
| 1–50 | 53 % | 40 % | 210 / 270 | 8.0 |
| 51–100 | 57 % | 44 % | 219 / 270 | 14.9 |
| 101–182 | 59 % | 45 % | 224 / 270 | 25.0 |
| 183–300 | 61 % | 47 % | 228 / 270 | 37.5 |
| 301–500 | 63 % | 49 % | 233 / 270 | 54.3 |

First 182 fights by N: 49 / 65 / 50 / 58 / 64 / 53 / 59 % group wins, 56.8 %
overall, 43.6 % catches per participant. That is close to the stream (one
standard error of 182 fights is ≈ 3.7 points); the rest of the gap likely
comes from the specimen pick up to 1.4.0 and PvP losses. Balancing variants
for WP-09 are compared with part 4 from now on; parts 2 and 3 overrate the
group. The repository's simulator runs the current rule; this table was
computed with the old rule restored locally for the run. Earlier versions of
part 4 drew the joins from the stream's per-viewer participation and from a
model fitted to it; their numbers differed by at most one point.

## Addendum 2026-11-19 (WP-09, boss targets and HP)

User decision („B2 mit 1.3“): the boss attacks `N / BossTargetDivisor`
participants per round **on average** — the remainder carried over, rounded up
first, `⌈r·N/d⌉ − ⌈(r−1)·N/d⌉` in round `r` with `d` = `BossTargetDivisor`
(`BossBattle.TargetsInRound`) — instead of `⌈N/d⌉` every round, and
`BossHpPerParticipant` goes from 1.5 to **1.3**. Planned for 1.8.0 (proposal,
not confirmed).

Cause of the `N = 4` dip: boss HP grows linearly with `N`, but `⌈N/3⌉` hits
1/2, 1/3, 1/2, 2/5, 1/3, 3/7 of the group per round for N = 2 … 7 — the
groups' win rates in the simulator follow that share almost exactly (on
stream only N = 4 stood out; 14 fights at N = 2 are too few to tell). The
carry-over keeps the share at one third for every `N`.

Simulator part 4, first 182 fights, group wins by N = 2 / 3 / 4 / 5 / 6 / 7 /
8, overall and catches per participant:

| Variant | N = 2 … 8 | Group wins | Catch/participant |
|---|---|---|---|
| ⌈N/3⌉, HP 1.5 (before 1.8.0) ‡ | 49 / 65 / 50 / 58 / 64 / 53 / 59 % | 56.8 % | 43.6 % |
| ⌈N/3⌉, HP 1.3 ‡ | 59 / 74 / 61 / 68 / 74 / 64 / 69 % | 67.0 % | 53.5 % |
| Rarity bonus 1.15 / 1.30 / 1.40 (common / rare / legendary), ⌈N/3⌉ † | 64 / 78 / 66 / 74 / 79 / 70 / 73 % | 72.1 % | 59.0 % |
| HP × (N/3) / ⌈N/3⌉, HP 1.5 † | 71 / 69 / 73 / 71 / 69 / 71 / 67 % | 70.8 % | 56.9 % |
| Carry-over rounding down, HP 1.5 † | 72 / 69 / 73 / 71 / 68 / 69 / 69 % | 70.7 % | 56.9 % |
| Carry-over rounding up, HP 1.5 | 62 / 67 / 66 / 66 / 67 / 65 / 66 % | 66.1 % | 51.9 % |
| Random rounding, HP 1.5 † | 69 / 68 / 69 / 68 / 69 / 67 / 66 % | 68.5 % | 54.6 % |
| Carry-over rounding down, HP 1.3 † | 80 / 77 / 80 / 79 / 78 / 78 / 76 % | 78.7 % | 65.5 % |
| **Carry-over rounding up, HP 1.3 (new)** | 70 / 76 / 75 / 74 / 75 / 74 / 73 % | **74.6 %** | **60.6 %** |
| Random rounding, HP 1.3 † | 76 / 77 / 78 / 77 / 77 / 77 / 74 % | 77.0 % | 63.6 % |

Unmarked rows: `--part 4` (with `--boss-hp 1.5`) as the repository runs it,
500 campaigns. ‡ the old rule restored locally for the run, otherwise the
same. † not reproducible from the repository: temporary engine or simulator
variants, 300 campaigns and the first participation profile — the rows
recomputed with the model moved by at most one point.

- Rounding up first lets the boss strike fully in round 1 and keeps the group
  sizes closest together; rounding down would leave a 2-player boss idle in
  round 1. Random rounding adds luck without a visible gain.
- The rarity bonus does not follow strength: many strong bosses are common
  (Knuddeluff, Lahmus, Sleimok), legendaries stay at 3–6 % (their attack and
  defence decide, not their HP), and its upper end (1.25 / 1.40 / 1.50) acts
  like a flat 1.3. HP compensation by target share makes a 4-player boss
  weaker than a 3-player one (4.0 × base HP vs. 4.5), visible on the fight
  page.
- With the new defaults, part 4 by phase: fights 1–50 71 %, 51–100 75 %,
  101–182 77 %, 183–300 78 %, 301–500 79 % group wins; by the boss's stat sum
  below 250 / 250–329 / 330–399 / from 400: 96 / 75 / 42 / 15 %; by tier
  common / rare / legendary 82 / 64 / 9 %. Part 2 (collections drawn like
  bosses, `--duels 1 --bosses 3000`): 79–89 % group wins, 70–75 % catches.
- The simulator ran ≈ 6 points above the stream with the old defaults, so
  the stream should land around 69 % group wins. Check after a few streams.
