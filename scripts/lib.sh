root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [ -f "$root/.envrc" ]; then
  pushd "$root" >/dev/null && source .envrc >/dev/null && popd >/dev/null
fi
