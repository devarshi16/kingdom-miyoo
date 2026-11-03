#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
T="$(mktemp -d)"; trap 'rm -rf "$T"' EXIT
mcs -optimize+ -out:"$T/cast.exe" -r:engine/build/UnityEngine.dll tests/ColliderCastRegression.cs
MONO_PATH="$PWD/engine/build" LD_LIBRARY_PATH="$PWD/engine/build" mono "$T/cast.exe" --allocation
if [ -n "${KNL_CAST_REFERENCE_RUNTIME:-}" ]; then
 MONO_PATH="$KNL_CAST_REFERENCE_RUNTIME" LD_LIBRARY_PATH="$KNL_CAST_REFERENCE_RUNTIME" mono "$T/cast.exe" > "$T/reference.txt"
 MONO_PATH="$PWD/engine/build" LD_LIBRARY_PATH="$PWD/engine/build" mono "$T/cast.exe" > "$T/candidate.txt"
 diff -u "$T/reference.txt" "$T/candidate.txt"
 echo 'PASS: 2064 casts retain exact reference hit order, distances, points and normals'
fi
