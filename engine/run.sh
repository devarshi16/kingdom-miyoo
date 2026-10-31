#!/usr/bin/env bash
# Run the desktop engine on the converted data. All arguments go to knl.exe.
#
# Play:
#   --boot --window              boot the real game from its loader scene in a 640x480 window, with sound
#     keys: arrows/A-D move, S/Down pay, Shift/C gallop, Enter/Space submit, Esc start, Tab select, F10 quit
#   --data DIR                   data folder (default ../extracted); a folder with kingdom.knlpack
#                                (tools/run.sh pack.py) runs from the device pack instead
#   --save DIR                   save folder (default engine/build/save)   --mute   no sound
#   KNL_CONTROLS=gamepad         controller glyphs instead of keyboard keys in hints (device default)
#
# Headless / debugging (--boot):
#   --frames N                   stop after N frames
#   --max-dt S                   longest real-time frame step (default 0.1 s; the Miyoo launcher uses 0.05)
#   --fixed-dt                   with --window: 1/30 s per frame like headless runs (deterministic, comparable shots)
#   --input "520-2300:right,2750-3300:down"   scripted buttons per frame range
#   --shot-every N --shots DIR   PNG every N frames;  --out f.png  last frame;  --wide-out f.png  2x wider view
#   --trace-scenes               every 30 frames: enemies, edge-indicator counts, king x, coins, day/time
#   --watch Type.field           print when a field changes;  --dump Type[,Type]  fields at the end
#   --inspect Name[,Tag]         objects, components, colliders at the end
#   --top N                      length of the exception / "needs" lists
#   --wav out.wav                capture the audio mix offline (headless)
#   KNL_UIDEBUG=1 / KNL_AUDIODEBUG=1   log canvas meshes / sound starts and mixer gains
#
# M1 viewer modes still work: --scenes level2,level1 --activate Blocks --camera X,Y --out frame.png
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(dirname "$HERE")"
# default data folder; relative --data/--save paths are taken from where you run this script
args=(); data="$ROOT/extracted"; next=""
for x in "$@"; do
  if [ -n "$next" ]; then
    case "$x" in /*) ;; *) x="$PWD/$x";; esac
    [ "$next" = data ] && data="$x" || args+=("--$next" "$x")
    next=""; continue
  fi
  case "$x" in --data) next=data;; --save) next=save;; *) args+=("$x");; esac
done
cd "$HERE/build"
LD_LIBRARY_PATH="$HERE/build${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}" exec mono knl.exe --data "$data" ${args[@]+"${args[@]}"}
