#!/usr/bin/env bash
# Status check for the agentic toolchain. Read-only: installs nothing.
# Usage: bash .claude/skills/ai-setup/scripts/check.sh   (from anywhere inside the repo)
set -u
root="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
cd "$root" || exit 1
fail=0
ok()   { printf '  OK    %s\n' "$1"; }
bad()  { printf '  FIX   %s  -> %s\n' "$1" "$2"; fail=1; }
warn() { printf '  WARN  %s  -> %s\n' "$1" "$2"; }

# Make ~/.dotnet/tools visible even if the shell profile doesn't add it (reported below).
dotnet_tools="$HOME/.dotnet/tools"

echo "[1] csharp-ls"
if command -v dotnet >/dev/null 2>&1; then
  if dotnet --list-sdks 2>/dev/null | grep -Eq '^(1[0-9])\.'; then ok "dotnet SDK >= 10 ($(command -v dotnet))"
  else bad "dotnet SDK 10+ missing on first dotnet in PATH ($(command -v dotnet))" "tools/csharp-ls.md#install"; fi
else bad "dotnet not found" "tools/csharp-ls.md#install"; fi
# bash expands a literal "~" in PATH, but Claude Code (not a shell) does not: look for a literal dir.
literal_hit=""
IFS=':' read -r -a path_dirs <<< "$PATH"
for d in "${path_dirs[@]}"; do [ -x "$d/csharp-ls" ] && case "$d" in "~"*) ;; *) literal_hit="$d"; break;; esac; done
if [ -n "$literal_hit" ]; then ok "csharp-ls on PATH ($(csharp-ls --version 2>/dev/null | head -1))"
elif command -v csharp-ls >/dev/null 2>&1; then bad "csharp-ls only reachable via a literal '~' PATH entry (Claude Code won't find it)" "tools/csharp-ls.md#configure"
elif [ -x "$dotnet_tools/csharp-ls" ]; then bad "csharp-ls installed but $dotnet_tools not on PATH" "tools/csharp-ls.md#configure"
else bad "csharp-ls not installed" "tools/csharp-ls.md#install"; fi
sln_count=$(find src -maxdepth 1 -name '*.sln' 2>/dev/null | wc -l | tr -d ' ')
if [ "$sln_count" = "1" ] && [ -f src/src.sln ]; then ok "single solution src/src.sln"
elif [ "$sln_count" = "0" ]; then bad "no .sln in src/ (Unity project files not generated)" "tools/csharp-ls.md#configure"
else bad "multiple .sln in src/ ($(ls src/*.sln | tr '\n' ' ')) - csharp-ls may load a stale one" "delete all but src/src.sln"; fi

if [ -L src/.claude ]; then
  if [ -f src/.claude/settings.json ]; then ok "src/.claude symlink -> $(readlink src/.claude)"
  else bad "src/.claude symlink is broken ($(readlink src/.claude))" "tools/csharp-ls.md#configure"; fi
elif [ -d src/.claude ]; then
  if [ "$(cd src/.claude && pwd -P)" = "$(cd .claude && pwd -P)" ]; then ok "src/.claude junction -> .claude"
  else bad "src/.claude is a real directory, not a link to ../.claude" "tools/csharp-ls.md#configure"; fi
elif [ -f src/.claude ]; then bad "src/.claude is a plain file (git checked the symlink out without symlink support)" "tools/csharp-ls.md#configure"
else bad "src/.claude missing" "tools/csharp-ls.md#configure"; fi

echo "[2] Unity CLI"
if command -v unity >/dev/null 2>&1; then
  ok "unity CLI $(unity --version 2>/dev/null | tail -1)"
  if unity status --json --no-banner 2>/dev/null | grep -q '"state": *"ready"'; then ok "Editor connected (ready)"
  else warn "no ready Editor connected" "open src/ in Unity if you need live Editor commands"; fi
else bad "unity CLI not found" "tools/unity-cli.md#install"; fi
if [ "$(uname)" = "Darwin" ]; then
  prefs=com.unity3d.UnityEditor5.x
  if [ "$(defaults read "$prefs" InteractionMode 2>/dev/null)" = "1" ]; then ok "Editor Interaction Mode = No Throttling"
  else bad "Editor Interaction Mode is not No Throttling (background Editors stall)" "tools/unity-cli.md#configure"; fi
  if [ "$(defaults read "$prefs" NSAppSleepDisabled 2>/dev/null)" = "1" ]; then ok "App Nap disabled for the Unity Editor"
  else bad "App Nap enabled for the Unity Editor" "tools/unity-cli.md#configure"; fi
fi

echo "[plugins]"
if command -v claude >/dev/null 2>&1; then
  plist="$(claude plugin list 2>/dev/null)"
  for p in csharp-lsp@claude-plugins-official unity@unity-agent-plugin; do
    if printf '%s\n' "$plist" | grep -A3 -F "$p" | grep -q 'enabled'; then ok "$p enabled"
    else bad "$p not installed/enabled" "claude plugin install $p --scope project"; fi
  done
else warn "claude CLI not on PATH" "cannot check plugins"; fi

echo
if [ $fail = 0 ]; then echo "All required tools OK."; else echo "Some items need fixing (see FIX lines)."; fi
exit $fail
