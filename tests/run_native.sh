#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
T="$(mktemp -d)"; trap 'rm -rf "$T"' EXIT
gcc -O3 -std=gnu99 tests/render_regression.c -o "$T/render" -lm
"$T/render"
bash engine/build_pouch.sh "${CXX:-g++}" "$T/pouch.o"
"${CXX:-g++}" -O2 -Iengine/native/third_party/box2d tests/pouch_regression.cpp "$T/pouch.o" -o "$T/pouch"
"$T/pouch"
