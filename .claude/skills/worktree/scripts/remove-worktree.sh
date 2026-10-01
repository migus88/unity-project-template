#!/usr/bin/env bash
# Close the worktree's Unity Editor, remove the worktree (with its Library) and optionally its branch.
# Usage: bash .claude/skills/worktree/scripts/remove-worktree.sh <name> [--delete-branch] [--force]
#   --delete-branch  git branch -d wt/<name> (refuses if unmerged; with --force: -D)
#   --force          remove even with uncommitted changes, and force-delete the branch
set -euo pipefail

name=""
delete_branch=0
force=0
while [ $# -gt 0 ]; do
  case "$1" in
    --delete-branch) delete_branch=1; shift ;;
    --force) force=1; shift ;;
    -h|--help) sed -n '2,5p' "$0"; exit 0 ;;
    *) name="$1"; shift ;;
  esac
done
[ -n "$name" ] || { echo "usage: remove-worktree.sh <name> [--delete-branch] [--force]" >&2; exit 2; }

main="$(git worktree list --porcelain | sed -n '1s/^worktree //p')"
main="$(cd "$main" && pwd -P)"
dir="$(dirname "$main")/$(basename "$main")-worktrees/$name"
branch="wt/$name"

if [ -d "$dir" ]; then
  dir="$(cd "$dir" && pwd -P)"
  if [ "$force" = 0 ] && [ -n "$(git -C "$dir" status --porcelain)" ]; then
    echo "worktree has uncommitted changes; commit them or pass --force:" >&2
    git -C "$dir" status --short >&2
    exit 1
  fi

  pid="$(unity status --format tsv --no-banner 2>/dev/null | awk -F'\t' -v p="$dir/src" '$3 == p { print $5 }' || true)"
  [ -n "$pid" ] || pid="$(sed -n 's/.*"process_id" : \([0-9]*\).*/\1/p' "$dir/src/Library/EditorInstance.json" 2>/dev/null || true)"
  if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
    echo "== closing Unity (pid $pid)"
    unity command eval --no-banner --project-path "$dir/src" --timeout 20 \
      --code 'UnityEditor.AssetDatabase.SaveAssets(); UnityEditor.EditorApplication.update += () => UnityEditor.EditorApplication.Exit(0); return "exiting";' >/dev/null 2>&1 || true
    for _ in $(seq 1 60); do kill -0 "$pid" 2>/dev/null || break; sleep 1; done
    if kill -0 "$pid" 2>/dev/null; then
      echo "   still running, sending SIGTERM"
      kill "$pid" 2>/dev/null || true
      for _ in $(seq 1 20); do kill -0 "$pid" 2>/dev/null || break; sleep 1; done
      kill -0 "$pid" 2>/dev/null && kill -9 "$pid" 2>/dev/null || true
    fi
  fi

  echo "== git worktree remove $dir"
  git -C "$main" worktree remove --force "$dir"
fi
git -C "$main" worktree prune

if [ "$delete_branch" = 1 ] && git -C "$main" show-ref --verify --quiet "refs/heads/$branch"; then
  if [ "$force" = 1 ]; then
    git -C "$main" branch -D "$branch"
  else
    git -C "$main" branch -d "$branch" || { echo "branch $branch is not merged; merge it or pass --force" >&2; exit 1; }
  fi
fi
rmdir "$(dirname "$dir")" 2>/dev/null || true
echo "removed $name"
