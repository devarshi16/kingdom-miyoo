#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
T="$(mktemp -d)"
trap 'rm -rf "$T"' EXIT
mcs -out:"$T/enemy-indicators.exe" -r:engine/build/UnityEngine.dll -r:engine/build/Assembly-CSharp.dll tests/EnemyIndicatorRegression.cs
MONO_PATH="$PWD/engine/build" LD_LIBRARY_PATH="$PWD/engine/build" mono "$T/enemy-indicators.exe"
