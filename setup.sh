#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
python3 -m venv .player-venv
.player-venv/bin/python -m pip install -r tools/requirements.txt
exec .player-venv/bin/python setup_gui.py "$@"
