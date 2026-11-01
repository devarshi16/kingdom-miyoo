#!/bin/sh
# Kingdom New Lands (KNL port) - OnionOS launcher. Run by the .port shortcut via launch_standalone.sh,
# or by hand over SSH: ./knl.sh [extra knl.exe args, e.g. --frames 600 --shot-every 300 --shots logs]
cd "$(dirname "$0")"
D="$PWD"
mkdir -p "$D/logs" "$D/save"
export LD_PRELOAD="/config/lib/libcam_os_wrapper.so"
export LD_LIBRARY_PATH="$D/lib:/config/lib:/customer/lib:/lib"
export MONO_PATH="$D/mono/lib/mono/4.5"
export MONO_GC_PARAMS=major=marksweep,nursery-size=16m
export KNL_CONTROLS=gamepad
cd managed
RUN_TARGET="knl.dll"
[ -f "knl.exe" ] && RUN_TARGET="knl.exe"
exec "$D/mono/bin/mono-sgen" "$RUN_TARGET" --data "$D/data" --save "$D/save" --boot --window --max-dt 0.05 "$@" \
  > "$D/logs/knl.log" 2>&1
