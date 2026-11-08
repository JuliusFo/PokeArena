---
name: release
description: Prepare and publish a PokeArena release (CHANGELOG, version, PR, local fast-forward merge, tag, release workflow)
disable-model-invocation: true
argument-hint: "[X.Y.Z]"
---
Talk to the user in German. The process is `CLAUDE.md` §7 ("Integration", "Release") and README "Releasing"; this skill
only runs it in order.

## 1. Where we stand

- `git fetch origin --tags`. Latest tag `vX.Y.Z`, `<Version>` in `Directory.Build.props`, the newest version section in
  `CHANGELOG.md`, and where `origin/main` is.
- Two cases:
  - **Already merged, tag missing**: `main` carries a CHANGELOG section and `<Version>` newer than the latest tag. Say
    so, ask for the OK to tag, and continue at step 5.
  - **Normal**: summarize the commits since the last tag (`git log vX.Y.Z..HEAD --oneline`, on the release branch) and
    the `[Unreleased]` entries. Propose the version (SemVer: fixes → patch, new commands or features → minor; a
    version from `$ARGUMENTS` wins) and wait for the OK.
- Ask whether the live test with this state has passed (`CLAUDE.md` §7: the CHANGELOG section comes after the live
  test). Without it, stop here.

## 2. Version and CHANGELOG on the branch

- Never on `main`. If this session is on `main`, create `docs/prepare-X.Y.Z` from `origin/main`; otherwise use the
  current package or feature branch.
- `CHANGELOG.md`: move the `[Unreleased]` entries into `## [X.Y.Z] - YYYY-MM-DD` below an empty `## [Unreleased]`.
  Sections in Keep-a-Changelog order (Added, Changed, Deprecated, Removed, Fixed, Security), empty ones left out.
  Entries in plain words for the streamer and players. Keep the file's line endings and encoding (no BOM).
- `<Version>X.Y.Z</Version>` in `Directory.Build.props`.
- If a default of `docs/spec.md` §13 or an NPC threshold changed in this release, check that `Pages/Guide.razor` says
  the same (spec §9).
- Update the status line and „Next“ in `CLAUDE.md` (version, what is done) and the package table in
  `docs/work-packages.md` if a package is released.
- `git diff --stat` to check, then one commit `docs: prepare X.Y.Z`.

## 3. Build and test

`dotnet build PokeArena.slnx -c Release -warnaserror`, then the four test projects as in `.github/workflows/ci.yml`.
Stop on any failure.

## 4. PR and merge

- Push the branch, `gh pr create --base main --body-file <file in the scratchpad>` (body: the CHANGELOG section).
- `gh pr checks --watch` until "Build and test (Windows)" is green. Never `--auto`, never `gh pr merge`: GitHub's merge
  rewrites commit ids.
- `git switch main`, `git pull --ff-only`, `git merge --ff-only <branch>`, `git push origin main`. If the
  fast-forward fails (`main` moved), stop and ask; do not rebase without the user's OK. GitHub marks the PR as merged
  by itself.

## 5. Tag

- On the up-to-date `main`, check once more that `Directory.Build.props` and the CHANGELOG section say `X.Y.Z`.
- `git tag -a vX.Y.Z -m "PokeArena X.Y.Z"` (annotated, like the earlier tags) and `git push origin vX.Y.Z`.

## 6. Release workflow

- `gh run watch` on the "Release" run of the tag until it finishes. It builds the self-contained ZIP and creates the
  GitHub release with the CHANGELOG section as notes.
- Check with `gh release view vX.Y.Z` that the ZIP `PokeArena-vX.Y.Z-win-x64.zip` is attached.
- If the website changed (`src/PokeArena.Web`, `src/PokeArena.Contracts`), check that the "Website" run on `main` is
  green too.
- List merged local branches and worktrees that could go; delete them only after the user confirms the list.

## 7. Finish

Short German summary: version, link to the GitHub release, what the streamer has to do (README "Updating": close the
app, replace the program folder with the new ZIP; the top bar shows „Version … verfügbar“ by itself). Suggest `/branch-code-review` on the next package branch if this
release went out without one.
