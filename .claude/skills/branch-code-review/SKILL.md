---
name: branch-code-review
description: Review the changes of the current feature branch with a separate reviewer agent, present the findings sorted by severity with proposed fixes, and fix the approved ones. Use when the user asks for a code review of the branch, the PR or "the changes".
argument-hint: "[focus, e.g. the mod commands]"
---
Talk to the user in German. Do not run any of this on `main`.

## 1. Check the branch

- `git branch --show-current`. Empty (detached HEAD) or `main`: refuse in one or two sentences ("Das Review prüft die Änderungen eines eigenen Branches; diese Session läuft auf …") and stop. Do not offer to create a branch.
- `git fetch origin main`, base = `git merge-base origin/main HEAD`. The scope is `base` to the working tree: committed changes plus uncommitted and untracked files (`git status --porcelain`). Nothing changed: say so and stop.
- If `origin/main` has moved past `base`, mention it; do not merge.
- Collect for the reviewer: changed files (`git diff --stat base`), the work package the branch belongs to (branch name `wp/NN-…`; its plan in `docs/roadmap.md`, its log entry in `docs/work-packages.md` if it exists), the `src/<Project>/CLAUDE.md` files of the projects the change touches, and the focus from `$ARGUMENTS`.

## 2. Run the reviewer

Spawn **one** separate agent (`general-purpose`). Never review inline in this session: the review must be independent of the code's author. Give it the facts, not your opinion of the code. The prompt names:

- Repository path, base SHA, HEAD SHA, and that uncommitted changes belong to the scope.
- `CLAUDE.md`, `.claude/skills/branch-code-review/checklist.md`, the matching `src/<Project>/CLAUDE.md` files, and the work package (roadmap section, log entry). For game logic or chat texts also `docs/spec.md` and `docs/chat-texts.md`; for structure `docs/architecture.md` and the ADRs the change touches.
- The focus from `$ARGUMENTS`, if any, as an extra priority – not as the only topic.
- Read-only: no edits, no commits, no checkout, stash or reset. It may run `dotnet build PokeArena.slnx -warnaserror` and the unit and component tests (`tests/PokeArena.Core.Tests`, `tests/PokeArena.UI.Tests`, `tests/PokeArena.Web.Tests`), not the integration tests. It never starts the desktop app against the real chat or the real data directory.
- The security pass of the checklist is mandatory: for every outside source the change touches, trace it to its sinks and name them, also when nothing is wrong.
- Review the changed code in its context (callers, sibling code, state it touches), and also what the change is missing (tests, `SiteOutbox` entries, `docs/chat-texts.md`, the guide page, spec, CHANGELOG, the other place that does the same thing).
- Pre-existing problems outside the change only if the change makes them reachable or worse; otherwise list them separately as "vorher schon da".
- Answer in German, one entry per finding: severity (S/M/K, checklist), location `path:line`, finding with a concrete failure scenario (input/state → wrong result), proposed fix, certainty (sicher / Verdacht). Then a short list "Ohne Befund geprüft" of what it checked and found sound.

Wait for its result. Do not review in parallel yourself.

## 3. Verify

Check every finding against the code yourself before presenting it. Mark it **nachgeprüft** (confirmed by reading the code), **Verdacht** (plausible, needs reproduction) or drop it as **verworfen** with a one-line reason (wrong reading, already handled, a user decision). Merge duplicates.

## 4. Present

As plain text in the chat (the wrap-up box is not visible on the phone), sorted S → M → K, numbered across all severities:

| # | Schwere | Ort | Befund | Lösung | Status |

Location as a clickable link. After the table: "Vorher schon da" (suggest `docs/backlog.md`), "Verworfen" (one line each), "Ohne Befund geprüft" (short), then questions where a fix needs a decision (game rules, chat wording, behaviour change). End with the question which points to fix (default: all). **Stop and wait for the approval.**

## 5. Fix the approved points

- In this session, without further agents. Order S → K.
- A **Verdacht** is reproduced first (a test, or the dry run of the desktop app with `--PokeArena:DataDirectory=<scratch> --PokeArena:Twitch:Channel=`); if it does not reproduce, change nothing and record why.
- Each bug fix with a test that fails without it, where feasible.
- `dotnet build PokeArena.slnx -warnaserror` and all four test projects green; website changes checked in the preview (`.claude/launch.json` „web“), desktop UI changes by bUnit or named as untested (no UI automation, `CLAUDE.md` §4).
- Small Conventional Commits (`fix:`, `test:`, `refactor:`, `docs:`).
- Second pass: send the same reviewer agent (`SendMessage`, it knows the findings) the range of the fix commits. It checks only those: does each fix cover its finding, did a fix break something else, is the test meaningful. Verify its new findings as in step 3; if any remain, present them as in step 4 and wait again.
- Protocol: in the package's entry in `docs/work-packages.md` add a bullet `- Code review (YYYY-MM-DD):` – the user's decision in their words, then one sub-bullet per finding (fixed / not reproduced / declined, with reason). A branch without a package (`feat/…`, `fix/…`) gets no protocol; mention the review in the PR description instead. Pre-existing findings the user wants kept go to `docs/backlog.md`. Adjust `CHANGELOG.md` only if a user-visible entry changes.
- Learnings: for each fixed finding, ask whether a note would have prevented it in the first implementation. If it is a trap that will come back in that area (not a one-off slip), add one line to the pitfalls in `src/<Project>/CLAUDE.md` of the project where the code lives, with the source in parentheses (`Review WP-NN` or the branch name). Otherwise add nothing: these files load into every session that touches the project and must stay short.
- Finish with a short German summary (fixed, open, what to test). Ask before pushing.
