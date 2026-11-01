#!/usr/bin/env bash
# Compile the pinned Box2D 2.3.1 source and the pouch bridge into one relocatable object.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
CXX="$1"; OUT="$2"; shift 2
mapfile -t SOURCES < <(find "$HERE/native/third_party/box2d/Box2D" -name '*.cpp' | sort)
"$CXX" -std=c++11 -O3 -fPIC -fno-exceptions -fno-rtti "$@" -I"$HERE/native/third_party/box2d" \
    -nostdlib -r "$HERE/native/knlpouch.cpp" "${SOURCES[@]}" -o "$OUT"
