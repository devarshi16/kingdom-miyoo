#!/usr/bin/env bash
# Run a tools/ script with the locally installed deps: tools/run.sh convert.py <args>
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
script="$1"; shift
if [ -f "$here/.venv/.knl_ok" ]; then
  exec "$here/.venv/bin/python" "$here/$script" "$@"
else
  PYTHONPATH="$here/.pydeps${PYTHONPATH:+:$PYTHONPATH}" exec python3 "$here/$script" "$@"
fi
