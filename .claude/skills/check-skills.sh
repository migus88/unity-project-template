#!/usr/bin/env bash
set -u

skills_dir="$(cd "$(dirname "$0")" && pwd)"
repo_root="$(cd "$skills_dir/../.." && pwd)"
project_root="$repo_root/src/Assets/_Project"
errors=0
notes=0

resolve() {
    local path="$1"
    local here="$2"
    [ -e "$here/$path" ] || [ -e "$repo_root/$path" ] || [ -e "$project_root/$path" ] || [ -e "$repo_root/src/$path" ]
}

is_path() {
    local token="$1"
    case "$token" in
        *" "*|*"<"*|*">"*|*"*"*|*"{"*|*"}"*|*'$'*|*"("*|*"|"*|http*) return 1 ;;
    esac
    case "${token%/}" in
        */*) ;;
        *) return 1 ;;
    esac
    case "$token" in
        */|*.cs|*.md|*.asmdef|*.asmref|*.unity|*.prefab|*.asset|*.json|*.inputactions|*.mixer|*.rsp|*.sh|*.ps1|*.txt|*.config) return 0 ;;
    esac
    return 1
}

for skill in "$skills_dir"/*/; do
    name="$(basename "$skill")"
    file="$skill/SKILL.md"
    if [ ! -f "$file" ]; then
        echo "ERROR $name: missing SKILL.md"
        errors=$((errors + 1))
        continue
    fi
    if ! sed -n '1,5p' "$file" | grep -qx "name: $name"; then
        echo "ERROR $name/SKILL.md: frontmatter 'name' must be '$name'"
        errors=$((errors + 1))
    fi
    if ! sed -n '1,5p' "$file" | grep -q '^description: .\{40,\}'; then
        echo "ERROR $name/SKILL.md: missing or too short 'description'"
        errors=$((errors + 1))
    fi
done

while IFS= read -r md; do
    rel="${md#"$repo_root"/}"
    base="$(basename "$md")"
    line_number=0
    while IFS= read -r line || [ -n "$line" ]; do
        line_number=$((line_number + 1))
        tokens="$(printf '%s\n' "$line" | grep -o '`[^`]*`' | tr -d '`')"
        [ -z "$tokens" ] && continue
        while IFS= read -r token; do
            is_path "$token" || continue
            resolve "$token" "$(dirname "$md")" && continue
            if [ "$base" = "examples.md" ] || printf '%s' "$line" | grep -qi 'if present'; then
                echo "note  $rel:$line_number: example path not found: $token"
                notes=$((notes + 1))
            else
                echo "ERROR $rel:$line_number: path not found: $token"
                errors=$((errors + 1))
            fi
        done <<< "$tokens"
    done < "$md"
done < <(find "$skills_dir" -name '*.md' -type f | sort; echo "$repo_root/docs/Architecture.md")

echo "check-skills: $errors error(s), $notes note(s)"
[ "$errors" -eq 0 ]
