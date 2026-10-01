# Worktree reference

Scripts: `scripts/new-worktree.sh`, `scripts/remove-worktree.sh` (macOS, Linux, Git Bash) and
`scripts/new-worktree.ps1`, `scripts/remove-worktree.ps1` (Windows PowerShell; untested).

## Layout and naming

| Item | Value |
|---|---|
| Worktree | `<parent of repo>/<repo>-worktrees/<name>`, e.g. `~/Repos/unity-project-template-worktrees/fix-audio` |
| Branch | `wt/<name>` from `HEAD` (or `--base <ref>`) |
| Unity project | `<worktree>/src` |
| Editor log | `<worktree>/src/Logs/Editor.log` (the shared `~/Library/Logs/Unity/Editor.log` belongs to the main Editor) |

Names: letters, digits, `.`, `_`, `-`. Outside the repo on purpose: Claude Code's own
.claude/worktrees folder would sit inside the repo and, through the `src/.claude` symlink, under
`src/` too, where greps, csharp-ls and tooling would pick it up.

## Library seeding

Measured on macOS (APFS, 1.7 GB Library): clone 9 s, Editor `ready` 31-38 s after launch,
settled (startup refresh done, `eval` answers) 38-62 s; only assets that differ from the main
checkout's working tree reimport. A fresh Library would mean a full import (many minutes).

| Excluded | Why |
|---|---|
| `ArtifactDB-lock`, `SourceAssetDB-lock` | LMDB locks of the running main Editor |
| `EditorInstance.json`, `ProtocolInstance.json`, `burst.pid`, `ilpp.pid` | PIDs/ports of the main Editor and its helpers (Rider protocol, Burst, IL post-processing) |
| `Pipeline/` | `.unity-pipeline-port` (main Editor's port and eval token) would point the CLI at the wrong Editor |
| `Bee/`, `BurstCache/`, `BuildHistory/`, `PackageManager/` | Hold absolute paths of the main checkout; rebuilt on open (one script compile, package re-resolve from `packages-lock.json`) |

Also copied: `src/Packages/nuget-packages/InstalledPackages` (git-ignored; NuGetForUnity would
otherwise have to restore it from the network before scripts compile) and `src/UserSettings`
(layout, editor user settings).

- macOS: `cp -c -R` makes APFS copy-on-write clones (no extra disk until files diverge); the
  script falls back to a plain copy across volumes. Linux: `cp --reflink=auto`.
- Windows: robocopy, a real copy (about the Library size on disk); exclusions via `/XD` `/XF`.
- The copy is a snapshot of a Library the main Editor may be writing. If the worktree Editor
  reports a corrupt asset database or misbehaves, close it, delete `<worktree>/src/Library` and
  reopen (full import).

## Packages and links

- Git-URL packages (e.g. MLock) are pinned by hash in `src/Packages/packages-lock.json` and come
  from the copied `Library/PackageCache`; a missing one is fetched again (needs `git` on PATH
  for the Editor). `file:` packages must use paths relative to `src/Packages`; an absolute
  `file:` path would point every worktree at the main checkout.
- `src/.claude` is a symlink to `../.claude`. Git on Windows without `core.symlinks=true`
  checks it out as a text file; the PowerShell script replaces it with a junction.
- .claude/settings.local.json is untracked: copy it by hand if the worktree session needs it.

## Opening and targeting

- macOS: `open -g -n -a <Unity.app> --args -projectPath <worktree>/src -logFile ...`. `-g`
  keeps the current app in front, `-n` forces a new instance. `unity open <path>` also works but
  brings the new Editor to the front.
- Windows: `Unity.exe -projectPath ... -logFile ...` via `Start-Process -WindowStyle Minimized`
  (Editor path from `docs/UnityCli.md`); falls back to `unity open`.
- `unity status` columns: Port, State, Project, Version, PID. Each Editor's pipeline server takes
  the next free port in 7800-7849 (main 7800, first worktree 7801, ...). `ready` only means the
  server is up: `eval` returns 503 "Server Busy" until the startup import/compile settles, hence
  the second wait in the script.
- Target resolution of `unity command`: `--project-path` (or `UNITY_PROJECT_PATH`), else the
  Editor whose project contains the cwd. There is no port flag; always pass `--project-path`.
- A Safe Mode or other startup dialog blocks the pipeline: the wait times out; read the log.

## Closing and cleanup

- `remove-worktree.*` exits the Editor with `eval` + `EditorApplication.update += () =>
  EditorApplication.Exit(0)`. Not `delayCall`: delayed calls do not run while the Editor is in
  the background (see `docs/UnityCli.md`). Measured: exit + removal in 8 s.
- `git worktree remove --force` deletes the whole folder including the ignored Library copy;
  the script first refuses if `git status --porcelain` shows changes.
- Branch: keep `wt/<name>` until it is merged (`git merge wt/<name>` or a PR from it); then
  `--delete-branch`. Unmerged branches are only deleted with `--force`.
- Stale entries (folder deleted by hand): `git worktree prune`.
