---
name: complete-code-review
description: Review the whole PokeArena solution with six parallel read-only agents by topic, write docs/review-X.Y.Z.md, present the findings by severity with proposed fixes, and implement the approved ones as an R package.
disable-model-invocation: true
argument-hint: "[focus]"
---
Talk to the user in German. The review document, the agents' reports and everything else written to the repository
are in English (`CLAUDE.md`: docs in English); quoted user words and German chat or UI texts stay as they are. Format
and wording follow the earlier reviews: read the newest `docs/review-*.md` first.
For a review of one branch's changes use `/branch-code-review` instead.

## 1. Prepare

- `git fetch origin main --tags`. The review reads `origin/main`; the session may sit on any branch with a clean
  working tree (uncommitted changes: say so and ask).
- Snapshot: note the SHA of `origin/main` and the released version (latest tag `vX.Y.Z`); the review is called `X.Y.Z`.
  Create a detached worktree of that SHA in the scratchpad (`git worktree add --detach <scratchpad>/snap <sha>`) and
  point every agent there – other sessions may switch branches in the shared copy. Remove it afterwards with
  `git worktree remove --force` and `git worktree prune`.
- Context for the agents: the last `docs/review-*.md` (findings, user decisions, "Status"), `docs/backlog.md`,
  `docs/roadmap.md`, the packages logged in `docs/work-packages.md` since that review, and
  `.claude/skills/branch-code-review/checklist.md`. Each agent also gets its brief from `agents.md` (next to this
  file).

## 2. Six agents by topic

Spawn six `general-purpose` agents in one message, in the background, by topic (not by folder), all read-only (no
edits, commits, checkout, stash, reset; never start the app or touch Twitch/R2). Only TE builds and runs tests.

| ID | Topic |
|---|---|
| GL | Game logic and data: Game, Commands, Data, migrations, spec and chat texts |
| TW | Twitch, chat queue, concurrency, desktop host, build and CI |
| WE | Publishing and outbox, website, overlay, desktop UI |
| SE | Security and privacy: the checklist's source → sink pass |
| TE | Tests: build, unit and bUnit tests, mutation checks, packages |
| RD | Reuse, dead code, structure, docs and `CLAUDE.md` files |

Every prompt contains: the snapshot path and SHA; that it is one of six parallel read-only reviewers; the required
reading (`CLAUDE.md`, the checklist, the `src/<Project>/CLAUDE.md` files of its topic, the docs of its topic, "User
decisions" in `docs/work-packages.md`); the agent's block from `agents.md` verbatim; the review points dead code,
duplicates, readability, architecture, security, structure, tests, docs as far as the topic covers them; the focus
from `$ARGUMENTS`, if any; "findings of the last review and the backlog only if their implementation is missing or
faulty; check that the last review's fixes took effect"; the answer format: English, IDs with the topic prefix (GL1,
GL2 …), per finding severity (H/M/L from the checklist), location `path:line`, finding with a concrete failure
scenario (input/state → wrong result), proposal, certainty (certain / suspected), for structure proposals the effort
(S/M/L = small/medium/large); then "Checked without findings"; no introduction, no conclusion.

Wait for all six. Verify the H and M findings of each agent as its report arrives.

## 3. Consolidate and write the document

- Merge duplicates (name all IDs). Verify every H and M finding of part A by reading the code: **confirmed**;
  where only the mechanism is confirmed and the trigger depends on Twitch, WPF, OBS or Cloudflare, say so ("path
  confirmed, trigger suspected"); otherwise the agent's assessment. Drop what contradicts a user decision.
- Write `docs/review-X.Y.Z.md` like the earlier ones: title with date; "Request" (quote the user's request in their
  words), the agents and the snapshot SHA; severity legend; build and test line; "Checked without findings
  (excerpt)"; **Part A – Bugs and security** (High / Medium with columns `# | IDs | Location | Finding | Certainty`,
  Low as `# | IDs | Finding`; each finding with a proposal); **Part B – Structure and reuse**
  (`# | IDs | Proposal | Effort`); **Part C – Tests** (gaps and weak tests, then "Redundant" with every test to
  merge or remove and the test that still catches its failure); **Part D – Docs and CLAUDE.md**; **Questions for the
  user**, always including scope and order (one package, or urgent fixes first as a patch).
- Create the branch `wp/r<n>-review-X.Y.Z` from `origin/main` (next free R number from `docs/work-packages.md`) and
  write the document there. Do not commit yet.

## 4. Present

As plain text in the chat (the wrap-up box is not visible on the phone): counts per part and severity, part A's
medium and severe findings in full with the proposed fixes, the small ones as a selection, parts B–D compact, then the
questions. **Stop and wait for the decisions.**

## 5. Implement

- Record the answers as "User decisions (YYYY-MM-DD)" in the review document (the user's answer quoted verbatim). Questions the user left
  open: take the proposal marked as default, say so in one line, and record it as such. Commit the document alone
  (`docs: add review X.Y.Z`).
- Implement as package R<n> on that branch, in this session without further agents. Order: docs that unblock others
  first (e.g. a missing skill), then part A with tests, B, C, D.
- A **suspected** finding is reproduced or checked against the external docs before any change; what does not reproduce goes
  into the log with the reason.
- Trimming tests: before removing or merging, make sure the remaining test fails when the code under test is broken
  the same way (break it briefly, run, restore). Note the test count before and after in the log.
- Done as in `CLAUDE.md` §7: `dotnet build PokeArena.slnx -warnaserror`, the four test projects (integration tests
  skip cleanly), `docs/chat-texts.md` and the guide page for changed texts or defaults, ADRs, log entry
  `### R<n> — Review X.Y.Z` in `docs/work-packages.md` (row in the table, user decisions, per finding fixed / not
  reproduced / moved to backlog), `CHANGELOG.md` under `[Unreleased]`, and the "Status" line in the review document.
  Kept-but-not-done items go to `docs/backlog.md`.
- Learnings: a trap that will come back in an area gets one line in `src/<Project>/CLAUDE.md` (source in
  parentheses, e.g. `Review 1.0.3`); one-off slips get nothing.
- Second pass: before pushing, run `/branch-code-review` on the package branch, so the fixes get their own
  independent review.
- Finish with a short German summary (done, open, to test). Ask before pushing; the release runs separately via
  `/release` after the live test.
