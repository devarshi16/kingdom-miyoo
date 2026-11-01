#!/usr/bin/env bash
set -euo pipefail
T="$(mktemp -d)"; trap 'rm -rf "$T"' EXIT
mcs -out:"$T/visual-regression.exe" -r:engine/build/UnityEngine.dll tests/VisualQualityRegression.cs
MONO_PATH="$PWD/engine/build" LD_LIBRARY_PATH="$PWD/engine/build" mono "$T/visual-regression.exe"
