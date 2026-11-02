#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
T="$(mktemp -d)"
trap 'rm -rf "$T"' EXIT
mapfile -t SOURCES < <(rg --files --no-ignore engine/UnityEngine -g '*.cs')
mcs -optimize+ -target:library -out:"$T/UnityEngine.dll" -nowarn:0108,0114,0618,0649,0169,0414,0067,0660,0661 -r:System.Core.dll "${SOURCES[@]}"
mcs -out:"$T/lite-weather-regression.exe" -r:"$T/UnityEngine.dll" tests/LiteWeatherRegression.cs
MONO_PATH="$T" mono "$T/lite-weather-regression.exe"
