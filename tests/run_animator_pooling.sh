#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
B="${KNL_TEST_ENGINE_DIR:-$PWD/engine/build}"
T="$(mktemp -d)"; trap 'rm -rf "$T"' EXIT
mcs -out:"$T/knl.exe" -r:"$B/UnityEngine.dll" tests/AnimatorPoolingRegression.cs
MONO_PATH="$B" mono "$T/knl.exe"
