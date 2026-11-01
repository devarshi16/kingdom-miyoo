#!/usr/bin/env bash
# Build a player archive from explicit, redistributable files only.
set -euo pipefail
HERE="$(cd "$(dirname "$0")/.." && pwd)"
exec python3 "$HERE/tools/package_release.py" "$@"
