#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
B="${KNL_TEST_ENGINE_DIR:-$PWD/engine/build}"
T="$(mktemp -d)"; trap 'rm -rf "$T"' EXIT
mcs -out:"$T/keyboard-regression.exe" -r:"$B/UnityEngine.dll" -r:"$B/UnityEngine.UI.dll" tests/KeyboardRegression.cs
MONO_PATH="$B" LD_LIBRARY_PATH="$B:$PWD/engine/build" mono "$T/keyboard-regression.exe"
