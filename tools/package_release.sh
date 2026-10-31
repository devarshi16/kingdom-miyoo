#!/usr/bin/env bash
# Creates a clean, redistributable GitHub Release zip without any copyrighted game data.
set -euo pipefail

HERE="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="$HERE/release_pkg"
ZIP_NAME="KingdomNewLands-MiyooMini-v1.0.zip"

rm -rf "$OUT_DIR" "$HERE/$ZIP_NAME"
mkdir -p "$OUT_DIR/Roms/PORTS/Shortcuts/Strategy"
mkdir -p "$OUT_DIR/Roms/PORTS/Games/KingdomNewLands/lib"
mkdir -p "$OUT_DIR/Roms/PORTS/Games/KingdomNewLands/mono"
mkdir -p "$OUT_DIR/Roms/PORTS/Games/KingdomNewLands/managed"
mkdir -p "$OUT_DIR/Roms/PORTS/Games/KingdomNewLands/data"
mkdir -p "$OUT_DIR/Roms/PORTS/Games/KingdomNewLands/save"
mkdir -p "$OUT_DIR/Roms/PORTS/Games/KingdomNewLands/logs"

GAME_SRC="$HERE/engine/build-miyoo/Roms/PORTS/Games/KingdomNewLands"

# Copy shortcut & launcher
cp "$HERE/engine/build-miyoo/Roms/PORTS/Shortcuts/Strategy/Kingdom New Lands.port" "$OUT_DIR/Roms/PORTS/Shortcuts/Strategy/"
cp "$GAME_SRC/knl.sh" "$OUT_DIR/Roms/PORTS/Games/KingdomNewLands/"

# Copy native libs
cp "$GAME_SRC/lib/"*.so* "$OUT_DIR/Roms/PORTS/Games/KingdomNewLands/lib/"

# Copy Mono runtime
cp -r "$GAME_SRC/mono/"* "$OUT_DIR/Roms/PORTS/Games/KingdomNewLands/mono/"

# Copy clean-room engine assemblies ONLY (no original game assemblies)
cp "$GAME_SRC/managed/UnityEngine.dll" "$OUT_DIR/Roms/PORTS/Games/KingdomNewLands/managed/"
cp "$GAME_SRC/managed/Rewired_Core.dll" "$OUT_DIR/Roms/PORTS/Games/KingdomNewLands/managed/"
cp "$GAME_SRC/managed/knl.exe" "$OUT_DIR/Roms/PORTS/Games/KingdomNewLands/managed/"

# Create placeholder README inside the port folder
cat << 'EOF' > "$OUT_DIR/Roms/PORTS/Games/KingdomNewLands/data/PLACE_DATA_HERE.txt"
Place your converted 'kingdom.knlpack' and 'music/' directory here.
Run './compile.sh /path/to/Kingdom_Data' from the source repo to generate them from your PC copy.
EOF

cd "$OUT_DIR"
zip -r -q "$HERE/$ZIP_NAME" Roms/
rm -rf "$OUT_DIR"

echo "Created release archive: $HERE/$ZIP_NAME"
