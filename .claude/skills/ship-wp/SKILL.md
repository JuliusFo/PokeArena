---
name: ship-wp
description: Implement a PokeArena work package from the roadmap end to end, from plan to green CI (plan, Core and tests, UI, docs, dry run, PR, branch review)
disable-model-invocation: true
argument-hint: "[WP-NN]"
---
Talk to the user in German; code, docs and commits in English. The rules are `CLAUDE.md` §7 ("Way of working",
"Integration"); this skill only runs them in order. Adapted from FerretSharp's `ship-wp`.

1. **Plan.** Read the package in `docs/roadmap.md` (from `$ARGUMENTS`, otherwise the next planned one in the table of
   `docs/work-packages.md`) with its user decisions and open questions, the log entries of earlier packages in the same
   area, and for game logic or chat texts `docs/spec.md` and `docs/chat-texts.md`. Create `wp/NN-short-name` from an
   up-to-date `origin/main`. Write the plan (what changes, where, tests, docs, open questions). If it is unclear *which*
   element is meant, ask; if only *how*, plan the simplest variant and name the question. **Wait for the approval.**
2. **Implement.** Core logic first with unit tests (`FakeTimeProvider`, seeded randomness), then UI (bUnit). In the same
   change: `docs/chat-texts.md` for every changed chat text, `Pages/Guide.razor` for a changed default of spec §13 or an
   NPC threshold, `docs/spec.md`, `docs/architecture.md`, an ADR for a non-trivial decision. Anything Claude proposes
   itself is written as "Proposal (not confirmed)". Small Conventional Commits.
3. **Build and test.** `dotnet build PokeArena.slnx -warnaserror` and the four test projects as in
   `.github/workflows/ci.yml` (integration tests skip cleanly without infrastructure). Fix until green.
4. **Check it running.** Never against the real data or the real chat.
   - Website (`src/PokeArena.Web`, `src/PokeArena.Contracts`): the preview `.claude/launch.json` "web" with the sample
     data.
   - Desktop host, DI, migrations, publishing: start the app once as a dry run with
     `--PokeArena:DataDirectory=<scratchpad>/data --PokeArena:Twitch:Channel=`, read its log in that directory for
     errors, then stop it.
   - Desktop UI behaviour: covered by bUnit; no UI automation (`CLAUDE.md` §4). What only the real app or a stream can
     show goes into "to test" in the summary.
5. **Docs of the package.** Row and log entry in `docs/work-packages.md` (with the user's decisions in their words),
   `CHANGELOG.md` under `[Unreleased]` in plain words for streamer and players, `README.md` if a command or visible
   feature changed, the status line and "Next" in `CLAUDE.md`. Ideas that came up go to `docs/backlog.md`. If players
   notice the change (commands, rules, durations, odds, website), add a line to `Pages/News.razor` under the upcoming
   version (newest on top, simple German, no date yet — `/release` sets it).
6. **PR.** Push the branch, `gh pr create --base main --body-file <file in the scratchpad>` (what, why, how tested,
   open questions).
7. **Review.** Run `/branch-code-review` (separate agent, findings by severity, wait for the approval, fix, second pass).
8. **CI.** Push and wait until "Build and test (Windows)" is green; fix what fails. Do not merge: the local
   `--ff-only` merge happens after the user's OK, the release via `/release` after the live test.
9. **Summary** in German: built, open, to test (including what to watch for in the next stream).
