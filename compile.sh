#!/usr/bin/env bash
# Kingdom New Lands — Compile & Package Script
# Builds the game engine and packages BYOD game assets for Miyoo Mini / desktop.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
DATA_DIR="${1:-}"

# Auto-detect game data directory if not provided
if [ -z "$DATA_DIR" ]; then
    for candidate in \
        "$HERE/Kingdom_Data" \
        "$HOME/Games/KingdomNewLands/Kingdom_Data" \
        "$HOME/.steam/steam/steamapps/common/Kingdom New Lands/Kingdom_Data" \
        "$HOME/.local/share/Steam/steamapps/common/Kingdom New Lands/Kingdom_Data" \
        "$HOME/GOG Games/Kingdom New Lands/Kingdom_Data"; do
        if [ -d "$candidate" ]; then
            DATA_DIR="$candidate"
            break
        fi
    done
fi

if [ -n "$DATA_DIR" ] && [ -d "$DATA_DIR/Kingdom_Data" ]; then
    DATA_DIR="$DATA_DIR/Kingdom_Data"
fi

if [ -z "$DATA_DIR" ] || [ ! -d "$DATA_DIR" ]; then
    echo "Usage: ./compile.sh <path/to/Kingdom_Data>"
    echo "Error: Kingdom_Data directory not found. Please provide path to game data."
    exit 1
fi

echo "=== 1. Setting up Python environment ==="
"$HERE/tools/setup.sh"

echo "=== 2. Extracting game assets from $DATA_DIR ==="
"$HERE/tools/run.sh" convert.py "$DATA_DIR"

echo "=== 3. Packing assets for device ==="
"$HERE/tools/run.sh" pack.py

echo "=== 4. Building game engine ==="
"$HERE/engine/build.sh" "$DATA_DIR"

# Check if Miyoo cross-compiler toolchain is available
TC="${MIYOO_TOOLCHAIN_ROOT:-$HOME/Recompilation/tools}"
CC="${MIYOO_CC:-$TC/prebuilt/bin/arm-linux-gnueabihf-gcc}"

if [ -x "$CC" ]; then
    echo "=== 5. Building Miyoo Mini package ==="
    "$HERE/engine/build_miyoo.sh"
    echo ""
    echo "SUCCESS: Miyoo Mini package built at engine/build-miyoo/Roms/"
    echo "Copy the 'Roms' folder from engine/build-miyoo/ directly to your SD card root."
else
    echo ""
    echo "=================================================================="
    echo "Desktop engine built at: engine/build/ (run: ./engine/run.sh --data pack --boot --window)"
    echo ""
    echo "[!] NOTE: Miyoo Mini package was NOT built because the ARM cross-compiler"
    echo "    was not found at: $CC"
    echo ""
    echo "    • If you want to play on Miyoo Mini without cross-compiling, use the"
    echo "      pre-built release package instead: python prepare_data.py \"$DATA_DIR\" --out \"/path/to/sdcard\""
    echo "    • To cross-compile the Miyoo package yourself, install the Miyoo toolchain"
    echo "      and set MIYOO_TOOLCHAIN_ROOT (or MIYOO_CC)."
    echo "=================================================================="
fi
