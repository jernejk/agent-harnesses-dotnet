#!/usr/bin/env bash
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
cd "$here/../../target"
source "$here/restore-on-exit.sh"

git bisect start HEAD v1.3.0
time git bisect run dotnet test --nologo -v q
