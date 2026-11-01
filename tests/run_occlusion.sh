#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
T="$(mktemp -d)"; trap 'rm -rf "$T"' EXIT
gcc -O3 -std=gnu99 tests/occlusion_regression.c -o "$T/occlusion" -lm
"$T/occlusion"
