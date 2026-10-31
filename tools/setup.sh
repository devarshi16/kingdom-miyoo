#!/usr/bin/env bash
# Install the PC-side Python dependencies locally (nothing system-wide).
# Uses tools/.venv when python3-venv works; otherwise falls back to tools/.pydeps
# (Ubuntu without the python3-venv package). Run scripts via tools/run.sh either way.
set -euo pipefail
cd "$(dirname "$0")"
if python3 -m venv .venv 2>/dev/null \
   && .venv/bin/python -m pip install -q --upgrade pip \
   && .venv/bin/python -m pip install -q -r requirements.txt; then
  touch .venv/.knl_ok   # run.sh only trusts a venv carrying this marker
  echo "ok: deps in tools/.venv"
else
  echo "python3-venv unavailable -> installing into tools/.pydeps"
  python3 -m pip install -q --target .pydeps -r requirements.txt
  echo "ok: deps in tools/.pydeps"
fi
command -v ffmpeg >/dev/null || echo "warning: ffmpeg not found; convert.py will need --skip audio"
echo "usage: tools/run.sh convert.py <Kingdom_Data>   |   tools/run.sh api_surface.py <Kingdom_Data>"
