#!/usr/bin/env bash
# Create a git worktree for parallel agent work, seed its Unity Library from the main checkout
# and open a new Unity Editor (with UI, in the background) on its src/.
# Usage: bash .claude/skills/worktree/scripts/new-worktree.sh <name> [--base <ref>] [--no-open] [--timeout <s>]
set -euo pipefail

name=""
base="HEAD"
open_editor=1
timeout_s=900
while [ $# -gt 0 ]; do
  case "$1" in
    --base) base="$2"; shift 2 ;;
    --no-open) open_editor=0; shift ;;
    --timeout) timeout_s="$2"; shift 2 ;;
    -h|--help) sed -n '2,4p' "$0"; exit 0 ;;
    *) name="$1"; shift ;;
  esac
done
case "$name" in
  ""|*[!A-Za-z0-9._-]*) echo "usage: new-worktree.sh <name: letters, digits, . _ -> [--base <ref>] [--no-open]" >&2; exit 2 ;;
esac

main="$(git worktree list --porcelain | sed -n '1s/^worktree //p')"
main="$(cd "$main" && pwd -P)"
parent="$(dirname "$main")/$(basename "$main")-worktrees"
dir="$parent/$name"
branch="wt/$name"
[ -e "$dir" ] && { echo "already exists: $dir" >&2; exit 1; }

echo "== git worktree add $dir ($branch from $base)"
mkdir -p "$parent"
git -C "$main" worktree add -b "$branch" "$dir" "$base"
dir="$(cd "$dir" && pwd -P)"

if [ ! -L "$dir/src/.claude" ]; then
  rm -rf "$dir/src/.claude"
  ln -s ../.claude "$dir/src/.claude"
fi

clone_dir() {
  local from="$1" to="$2"
  [ -d "$from" ] || return 0
  mkdir -p "$(dirname "$to")"
  if [ "$(uname)" = "Darwin" ]; then
    cp -c -R "$from" "$to" 2>/dev/null || { rm -rf "$to"; cp -R "$from" "$to"; }
  else
    cp -R --reflink=auto "$from" "$to"
  fi
}

echo "== seeding Library from $main/src/Library"
t0=$(date +%s)
clone_dir "$main/src/Library" "$dir/src/Library"
( cd "$dir/src/Library" && rm -rf Bee BurstCache BuildHistory PackageManager Pipeline \
    ArtifactDB-lock SourceAssetDB-lock EditorInstance.json ProtocolInstance.json burst.pid ilpp.pid )
clone_dir "$main/src/Packages/nuget-packages/InstalledPackages" "$dir/src/Packages/nuget-packages/InstalledPackages"
clone_dir "$main/src/UserSettings" "$dir/src/UserSettings"
echo "   copied in $(( $(date +%s) - t0 )) s ($(du -sh "$dir/src/Library" | cut -f1) logical)"

echo "worktree: $dir"
echo "branch:   $branch"
echo "project:  $dir/src"
[ "$open_editor" = 1 ] || exit 0

version="$(sed -n 's/^m_EditorVersion: //p' "$main/src/ProjectSettings/ProjectVersion.txt")"
echo "== opening Unity $version on $dir/src"
t0=$(date +%s)
if [ "$(uname)" = "Darwin" ]; then
  app="/Applications/Unity/Hub/Editor/$version/Unity.app"
  [ -d "$app" ] || app="$(unity editors -i --json --no-banner | grep -A3 "\"version\": \"$version\"" | sed -n 's/.*"location": "\(.*\)".*/\1/p' | head -1)"
  mkdir -p "$dir/src/Logs"
  open -g -n -a "$app" --args -projectPath "$dir/src" -logFile "$dir/src/Logs/Editor.log"
else
  unity open "$dir/src" --no-banner --args "-logFile $dir/src/Logs/Editor.log"
fi

fail() {
  echo "$1 after ${timeout_s}s; check $dir/src/Logs/Editor.log and the Editor window (Safe Mode, dialog?)" >&2
  exit 1
}

echo "== waiting for 'unity status' to list it ready (timeout ${timeout_s}s)"
while :; do
  state="$(unity status --format tsv --no-banner 2>/dev/null | awk -F'\t' -v p="$dir/src" '$3 == p { print $2 "\t" $1 "\t" $5 }' || true)"
  case "$state" in
    ready*) printf '   ready after %s s: port %s, pid %s\n' "$(( $(date +%s) - t0 ))" "$(echo "$state" | cut -f2)" "$(echo "$state" | cut -f3)"; break ;;
  esac
  [ $(( $(date +%s) - t0 )) -lt "$timeout_s" ] || fail "not listed ready (state: ${state:-none})"
  sleep 5
done

echo "== waiting for the startup import/compile to settle (main-thread commands answer)"
while :; do
  if unity command eval --no-banner --project-path "$dir/src" --timeout 20 --code 'return "settled";' 2>/dev/null | grep -q '"result":"settled"'; then
    echo "   settled after $(( $(date +%s) - t0 )) s"
    break
  fi
  [ $(( $(date +%s) - t0 )) -lt "$timeout_s" ] || fail "main-thread commands still busy"
  sleep 5
done
echo "drive it with: unity command <name> --no-banner --project-path $dir/src"
