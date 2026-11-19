# Boss targets and HP from stream data — user request (2026-11-19, branch feat/boss-balancing)

- Built: an evaluation of the 182 published boss fights (50.5 % group wins,
  37.7 % catches per participant, N = 4 34 %; script kept outside the repo,
  numbers in the ADR-0006 addendum). Simulator part 4 (`--part 4`): campaigns
  in which 12 trainers own a starter and only the bosses they survived, lobby
  sizes as on stream, joins by a calibrated small-channel model — it reproduces the stream (57 % / 44 %), parts 2
  and 3 overrate the group (≈ 80 %); options `--part`, `--campaigns`,
  `--encounters`, boss HP per tier. Core: `BossBattle.TargetsInRound` —
  `⌈r·N/3⌉ − ⌈(r−1)·N/3⌉` targets in round r instead of `⌈N/3⌉`;
  `BossHpPerParticipant` 1.3. Part 4 now: 75 % group wins, 61 % catches,
  70–76 % for every N. Spec §4/§13, ADR-0006 addendum, roadmap WP-09.
- User decisions: „B2 mit 1.3“ (carry-over rounding up first, HP 1.3) after
  comparing the rarity HP bonus (user idea), HP compensation by target share,
  carry-over rounding down and random rounding (all in the ADR table).
- Addenda: version 1.8.0 is a proposal (not confirmed) — it is the WP-09
  balancing number. The „Neuigkeiten“ test allows one undated section, so the
  unreleased 1.7.2 lines of `main` moved under „Version 1.8.0“; `/release` decides.
- Code review (2026-11-19): „Alle beheben“.
  - L→M #1 per-viewer participation counts of the 12 stream trainers in the
    simulator (public repository, matchable through the „Statistik“ page):
    fixed — a small-channel model (each trainer joins 0.85 × as often as the
    one before, not fitted to anyone) replaces them; the branch is rewritten
    locally so no commit holds them (old commits stay on GitHub until a
    force-push, see the second pass).
  - #2 drawing a lobby one by one evened out the join rates (top trainers
    ≈ 20 % too rare): fixed — the join weights are calibrated on fixed seeds
    until the simulated rates match the model (printed with every run).
    Numbers moved by at most one point.
  - #3 ADR rows not reproducible from the repository: fixed — rows marked
    (‡ old rule restored locally, † temporary variants), the reproducible
    ones recomputed.
  - #4 „groups of 2 or 4 had it harder“ held only in the simulator: fixed in
    „Neuigkeiten“, spec and the doc comment (on stream: groups of 4).
  - #5 1.8.0 written as fact: fixed — „planned for 1.8.0 (proposal, not
    confirmed)“; roadmap heading of the HP-before fix names both versions.
  - #6 ADR-0006 „Decision“ stale: fixed with pointers to the new addendum.
  - #7 spec details (182 fights, `d` in the formula, 79–89 % in part 2):
    fixed.
  - #8 `TargetsInRound` wrong for round ≤ 0: fixed with guards and a test
    that fails without them.
  - #9 no fight test for a round without a boss attack: test added (N = 2,
    round 3, heading kept).
  - #10 simulator „NaN“ rows and `--part 5`: fixed („-“ for empty rows,
    `--part` and a missing option value are refused).
  - Second pass: the fixes hold; three new points. N1 (M) the old commits
    with the counts are still public on origin and in PR #57 — a force-push
    moves the refs, but GitHub keeps commits a PR references (only GitHub
    Support purges them): decision for the user. N2 (L) the first model was
    fitted per rank to the removed counts:
    fixed with the round decay 0.85ⁿ; numbers moved by at most one point.
    N3 (L) two stale roadmap remarks („changed in 1.8.0“, „target ≈ 80 %“):
    fixed.
- Pitfalls: the published fight files are rate-limited by Cloudflare — fetch
  them a few at a time.
- Deliberately not done (→ roadmap WP-09): a boss HP that follows the boss's
  strength; PvP loot and NPC difficulty.
