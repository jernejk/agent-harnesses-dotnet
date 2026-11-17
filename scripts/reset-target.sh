#!/usr/bin/env bash
# Rebuilds target/ as a throwaway git repo whose last commits introduce the regression.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
seed="$root/target-seed"
target="$root/target"

source "$root/scripts/lib.sh"
"$root/scripts/orders.sh" stop >/dev/null 2>&1 || true

rm -rf "$target" "$root/proposals" "$root/.sessions" "$root/.run/memory"
rm -f "$root/.run/05-usage.json" "$root/.run/05-trace.jsonl"
mkdir -p "$target" "$root/proposals"
cd "$target"

git init -q -b main
git config commit.gpgsign false
git config tag.gpgsign false
git config core.hooksPath /dev/null

while IFS='|' read -r step date name email message tag; do
  [ -z "$step" ] && continue
  cp -R "$seed/$step/." "$target/"
  git add -A
  GIT_AUTHOR_NAME="$name" GIT_AUTHOR_EMAIL="$email" GIT_AUTHOR_DATE="$date" \
  GIT_COMMITTER_NAME="$name" GIT_COMMITTER_EMAIL="$email" GIT_COMMITTER_DATE="$date" \
    git commit -q -m "$message"
  [ -n "$tag" ] && git tag "$tag"
done < "$seed/history.txt"

dotnet build --nologo -v q >/dev/null
git log --oneline --decorate
"$root/scripts/orders.sh" start
