#!/usr/bin/env bash
# Build the desktop engine: native core + our UnityEngine.dll and Rewired_Core.dll (two-stage
# each) + knl.exe host. Your game assemblies are copied unmodified.
#
#   engine/build.sh [path/to/Kingdom_Data]      (default: ~/Games/KingdomNewLands/Kingdom_Data)
#
# Needs: mono-devel (mcs + mono, tested 6.8), gcc/g++, python deps (tools/setup.sh),
#        libsdl2-dev for window + sound (without it you get a headless, PNG-only core).
# Run tools/run.sh convert.py first so ../extracted exists (sprites + objects + audio).
# Third party: native/third_party/stb_image.h, stb_image_write.h, stb_vorbis.c (stb commit f58f558c).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(dirname "$HERE")"
GAME="${1:-$HOME/Games/KingdomNewLands/Kingdom_Data}"
M="$GAME/Managed"
REPORT="${KNL_EXTRACTED:-$ROOT/extracted}/report.json"
B="$HERE/build"
NOWARN=0108,0114,0618,0649,0169,0414,0067,0660,0661
SHIM() { bash "$ROOT/tools/run.sh" gen_shim.py "$@"; }

for t in mcs mono gcc python3 "${CXX:-g++}"; do command -v $t >/dev/null || { echo "missing: $t (sudo apt install mono-devel build-essential)"; exit 1; }; done
[ -f "$M/Assembly-CSharp.dll" ] || { echo "no game assemblies at $M"; exit 1; }
[ -f "$REPORT" ] || { echo "run tools/run.sh convert.py $GAME first"; exit 1; }
mkdir -p "$B"
rm -f "$B"/*.dll "$B/knl.exe"
cd "$HERE"

echo "== native core"
T="$(mktemp -d)"; trap 'rm -rf "$T"' EXIT
bash "$HERE/build_pouch.sh" "${CXX:-g++}" "$T/pouch.o"
if pkg-config --exists sdl2 2>/dev/null; then
  for src in knlcore knlaudio; do gcc -O2 -std=c99 -fPIC -DKNL_SDL $(pkg-config --cflags sdl2) -c "native/$src.c" -o "$T/$src.o"; done
  "${CXX:-g++}" -shared "$T"/*.o -o "$B/libknlcore.so" $(pkg-config --libs sdl2) -lm
else
  echo "   (libsdl2-dev not found: headless build, no window or sound)"
  for src in knlcore knlaudio; do gcc -O2 -std=c99 -fPIC -c "native/$src.c" -o "$T/$src.o"; done
  "${CXX:-g++}" -shared "$T"/*.o -o "$B/libknlcore.so" -lm
fi

echo "== UnityEngine.dll (stage 1: hand-written + generated declarations)"
SHIM "$M" UnityEngine/Generated/Skeleton.g.cs --hand UnityEngine --extra-types "$REPORT" >/dev/null
mcs -optimize+ -target:library -out:"$B/UnityEngine.stage1.dll" -nowarn:$NOWARN -r:System.Core.dll $(find UnityEngine -name "*.cs")
echo "== UnityEngine.dll (stage 2: + logging stubs for everything else the game references)"
# stage 2 reads stage 1 back through tools/stage1dump.cs (Mono reflection); a failure aborts the build
SHIM "$M" UnityEngine/Generated/Skeleton.g.cs --hand UnityEngine --extra-types "$REPORT" --stage1 "$B/UnityEngine.stage1.dll"
mcs -optimize+ -target:library -out:"$B/UnityEngine.dll" -nowarn:$NOWARN -r:System.Core.dll $(find UnityEngine -name "*.cs")

echo "== Rewired_Core.dll (clean-room replacement, D11; same two stages)"
SHIM "$M" Rewired/Generated/Skeleton.g.cs --hand Rewired --target Rewired_Core >/dev/null
mcs -optimize+ -target:library -out:"$B/Rewired_Core.stage1.dll" -nowarn:$NOWARN -r:"$B/UnityEngine.dll" -r:System.Core.dll $(find Rewired -name "*.cs")
SHIM "$M" Rewired/Generated/Skeleton.g.cs --hand Rewired --target Rewired_Core --stage1 "$B/Rewired_Core.stage1.dll"
mcs -optimize+ -target:library -out:"$B/Rewired_Core.dll" -nowarn:$NOWARN -r:"$B/UnityEngine.dll" -r:System.Core.dll $(find Rewired -name "*.cs")

echo "== game assemblies (yours, unmodified; Unity/BCL/Rewired ones are replaced)"
for f in "$M"/*.dll; do
  case "$(basename "$f")" in
    UnityEngine.dll|mscorlib.dll|System*.dll|Mono.Security.dll|Rewired_Core.dll|Rewired_Windows_Lib.dll) ;;
    *) cp "$f" "$B/" ;;
  esac
done
rm -f "$B/Rewired_Windows_Lib.dll"

echo "== knl.dll (managed executable)"
mcs -optimize+ -target:exe -out:"$B/knl.dll" -r:"$B/UnityEngine.dll" -r:System.Core.dll Host/Program.cs
echo "ok -> $B   (try: engine/run.sh --boot --window; for device data: tools/run.sh pack.py, then engine/run.sh --data pack --boot --window)"
