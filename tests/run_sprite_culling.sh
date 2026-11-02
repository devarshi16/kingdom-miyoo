#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
B="${KNL_TEST_ENGINE_DIR:-$PWD/engine/build}"
T="$(mktemp -d)"
trap 'rm -rf "$T"' EXIT
# Friend assembly name exposes internal geometry only to this isolated developer fixture.
mcs -out:"$T/knl.exe" -r:"$B/UnityEngine.dll" tests/SpriteCullingRegression.cs
MONO_PATH="$B" LD_LIBRARY_PATH="$B:$PWD/engine/build" mono "$T/knl.exe"
