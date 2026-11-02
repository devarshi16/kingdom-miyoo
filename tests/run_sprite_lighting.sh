#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
B="${KNL_TEST_ENGINE_DIR:-$PWD/engine/build}"
T="$(mktemp -d)"; trap 'rm -rf "$T"' EXIT
mcs -out:"$T/sprite-lighting.exe" -r:"$B/UnityEngine.dll" tests/SpriteLightingRegression.cs
MONO_PATH="$B" LD_LIBRARY_PATH="$B:$PWD/engine/build" mono "$T/sprite-lighting.exe"
