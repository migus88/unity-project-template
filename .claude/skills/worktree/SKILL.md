---
name: worktree
description: Create, drive and remove a git worktree with its own Unity Editor for parallel agent work in this repo - the worktree in ../<repo>-worktrees/<name> on branch wt/<name>, Library/ cloned from the main checkout so nothing reimports, a second Unity Editor (with UI, opened in the background) on the worktree's src/, targeting it with `unity command --project-path`, and cleanup (close the Editor, git worktree remove, branch). Use FIRST whenever an agent is asked to work in a separate/isolated worktree, in parallel with another agent, on a separate branch with its own Unity Editor, or must not disturb the main checkout's Editor.
---

# Worktree with its own Unity Editor

One worktree = one branch = one Unity Editor. The main checkout's Editor keeps running; never
drive it from worktree work. Details, timings and pitfalls: [reference.md](reference.md).

## Create

```bash
bash .claude/skills/worktree/scripts/new-worktree.sh <name> [--base <ref>] [--no-open]
```
```powershell
pwsh -File .claude/skills/worktree/scripts/new-worktree.ps1 <name> [-Base <ref>] [-NoOpen]
```

The script:
1. `git worktree add -b wt/<name> ../<repo>-worktrees/<name> <base>` (outside the repo: no
   ignore rules needed, invisible to Unity, grep and the main LSP). Base defaults to `HEAD`;
   uncommitted changes in the main checkout are not carried over.
2. Fixes the `src/.claude` link (symlink; junction on Windows).
3. Clones `src/Library` (APFS clone `cp -c` on macOS, robocopy on Windows) without lock, pid,
   instance and port files and without path-bound caches (`Bee`, `BurstCache`, `BuildHistory`,
   `PackageManager`, `Pipeline`); also copies NuGet `InstalledPackages` and `UserSettings`.
4. Opens Unity in the background (`open -g -n` on macOS, so focus is not stolen) with
   `-logFile src/Logs/Editor.log`, waits until `unity status` lists it `ready`, then until a
   trivial `eval` answers (startup import/compile settled).

## Work in it

- Edit files only under `../<repo>-worktrees/<name>/` (absolute paths); commit on `wt/<name>`.
- Always target the worktree Editor explicitly (several Editors are running):
  `unity command <cmd> --no-banner --project-path <worktree>/src`. Without the flag the CLI
  picks the Editor whose project contains the cwd and fails with `AMBIGUOUS_EDITOR` elsewhere.
- Same verification loop as in the main checkout (`docs/UnityCli.md`): refresh, `recompile`,
  poll `recompile_status`, `console --level error`, `run_tests` + `test_status`.
- Never `editor_focus` or `recompile --focus true`; background Editors work unfocused.
- The main session's `LSP` indexes the main checkout only; use Grep/Read in the worktree.

## Remove

```bash
bash .claude/skills/worktree/scripts/remove-worktree.sh <name> [--delete-branch] [--force]
```
```powershell
pwsh -File .claude/skills/worktree/scripts/remove-worktree.ps1 <name> [-DeleteBranch] [-Force]
```

Refuses on uncommitted changes (unless forced), saves assets and exits that Editor via
`eval` (falls back to killing it after 60 s), `git worktree remove --force` (deletes the
Library copy with it), `git worktree prune`, and with `--delete-branch` runs `git branch -d`
(`-D` with `--force`). Merge or push `wt/<name>` first if the work should survive; never delete
someone else's worktree.
