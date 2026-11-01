#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
T="$(mktemp -d)"; trap 'rm -rf "$T"' EXIT
mcs -out:"$T/counter-regression.exe" -r:engine/build/UnityEngine.dll tests/CoinCounterRegression.cs
KNL_WALLET=counter MONO_PATH="$PWD/engine/build" LD_LIBRARY_PATH="$PWD/engine/build" mono "$T/counter-regression.exe"
KNL_WALLET=physical MONO_PATH="$PWD/engine/build" LD_LIBRARY_PATH="$PWD/engine/build" mono "$T/counter-regression.exe" physical
mcs -out:"$T/overflow-regression.exe" -r:engine/build/UnityEngine.dll tests/CounterOverflowRegression.cs
MONO_PATH="$PWD/engine/build" mono "$T/overflow-regression.exe"
mcs -nowarn:0067 -out:"$T/hud-regression.exe" -r:engine/build/UnityEngine.dll tests/CounterHudRegression.cs
KNL_WALLET=counter MONO_PATH="$PWD/engine/build" LD_LIBRARY_PATH="$PWD/engine/build" mono "$T/hud-regression.exe"
