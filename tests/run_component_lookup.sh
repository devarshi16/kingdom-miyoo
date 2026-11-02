#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
T="$(mktemp -d)"; trap 'rm -rf "$T"' EXIT
mcs -out:"$T/component-lookup.exe" -r:engine/build/UnityEngine.dll tests/ComponentLookupRegression.cs
MONO_PATH="$PWD/engine/build" LD_LIBRARY_PATH="$PWD/engine/build" mono "$T/component-lookup.exe"
