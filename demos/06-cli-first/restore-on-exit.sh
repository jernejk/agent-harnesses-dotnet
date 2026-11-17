# Sourced by bisect.sh: stash local changes now, put them back however the script ends.
stash=$(git stash create)
if [ -n "$stash" ]; then
  git stash store -m bisect.sh "$stash"
  git reset --hard --quiet
fi

restore() {
  git bisect reset >/dev/null 2>&1 || true
  [ -n "$stash" ] || return 0
  if git stash apply --quiet "$stash"; then
    entry=$(git stash list --format='%gd %H' \
      | awk -v sha="$stash" '$2 == sha { print $1 }')
    [ -z "$entry" ] || git stash drop --quiet "$entry"
  else
    echo "bisect.sh: local changes kept in git stash ($stash)" >&2
  fi
}
trap restore EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
