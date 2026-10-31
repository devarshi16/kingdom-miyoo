#!/usr/bin/env bash
# Build the Miyoo Mini Plus (OnionOS) port folder (D21). Run engine/build.sh and tools/run.sh pack.py first:
# the managed DLLs are the same IL as desktop, only libknlcore.so is cross-compiled.
#
#   engine/build_miyoo.sh [OUT]            (default: engine/build-miyoo/)   -> OUT/Roms/PORTS/...
#   engine/build_miyoo.sh --push           same, then copy it to the device over SSH (host alias "miyoo")
#
# Tools (not in this repo; override the paths with env vars):
#   MIYOO_CC     ARM GNU Toolchain 8.2-2018-08 arm-linux-gnueabihf-gcc 8.2.1 (matches the device's glibc 2.28)
#   MIYOO_MI_INC SigmaStar MI headers (mi_sys.h, mi_ao.h) from steward-fu/sdl2 mini/inc (commit 0631abc)
#   MIYOO_MI_LIB libmi_sys.so / libmi_ao.so to link against (the device's own copies in /config/lib are used at run time)
#   MONO_ROOT    extracted Mono 6.8.0.105 armhf packages (download.mono-project.com, stable-buster snapshot
#                6.8.0.105, 0xamarin3+debian10b1): mono-runtime-sgen, mono-runtime-common, libmono-corlib4.5-cil,
#                libmono-system{,-core,-xml,-configuration}4.0-cil, libmono-security4.0-cil
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(dirname "$HERE")"
TC="${MIYOO_TOOLCHAIN_ROOT:-$HOME/Recompilation/tools}"
CC="${MIYOO_CC:-$TC/prebuilt/bin/arm-linux-gnueabihf-gcc}"
MI_INC="${MIYOO_MI_INC:-$TC/sdl2-miyoo/mini/inc}"
MI_LIB="${MIYOO_MI_LIB:-$TC/sdl2-miyoo/mini/lib}"
MONO_ROOT="${MONO_ROOT:-$TC/mono-armhf-6.8.0.105/root}"
PUSH=0; OUT="$HERE/build-miyoo"
for x in "$@"; do case "$x" in --push) PUSH=1;; *) OUT="$x";; esac; done
B="$HERE/build"; GAME="$OUT/Roms/PORTS/Games/KingdomNewLands"
[ -f "$B/knl.exe" ] || { echo "run engine/build.sh first"; exit 1; }
[ -f "$ROOT/pack/kingdom.knlpack" ] || { echo "run tools/run.sh pack.py first"; exit 1; }

echo "== libknlcore.so (armhf, Cortex-A7 NEON hard-float)"
mkdir -p "$GAME/lib" "$GAME/managed" "$GAME/mono/bin" "$GAME/mono/lib/mono/4.5" "$GAME/mono/etc/mono" "$GAME/data" "$GAME/save" "$GAME/logs" \
         "$OUT/Roms/PORTS/Shortcuts/Strategy"
F="-O3 -std=gnu99 -fPIC -mcpu=cortex-a7 -mfpu=neon-vfpv4 -mfloat-abi=hard -marm"
T="$(mktemp -d)"; trap 'rm -rf "$T"' EXIT
"$CC" $F -DKNL_MIYOO -c "$HERE/native/knlcore.c" -o "$T/knlcore.o"
"$CC" $F -DKNL_MIYOO -I"$MI_INC" -c "$HERE/native/knlaudio.c" -o "$T/knlaudio.o"
"$CC" -shared -s -Wl,-soname,libgssapi_krb5.so.2 -Wl,--version-script,"$HERE/miyoo/gssapi_stub.map" \
     "$HERE/miyoo/gssapi_stub.c" -o "$GAME/lib/libgssapi_krb5.so.2"
"$CC" -shared -s "$T/knlcore.o" "$T/knlaudio.o" -o "$GAME/lib/libknlcore.so" -L"$MI_LIB" -lmi_ao -lmi_sys -lpthread -lm

echo "== Mono runtime + the BCL assemblies the game loads"
# mono finds lib/ and etc/ next to bin/ ($mono_libdir in etc/mono/config); FAT32 has no symlinks, so real names
cp "$MONO_ROOT/usr/bin/mono-sgen" "$GAME/mono/bin/"
cp "$MONO_ROOT/usr/lib/libmono-native.so.0.0.0" "$GAME/mono/lib/libmono-native.so"
cp "$MONO_ROOT/etc/mono/config" "$GAME/mono/etc/mono/"
for a in mscorlib System System.Core System.Xml System.Configuration Mono.Security; do
  cp "$MONO_ROOT/usr/lib/mono/4.5/$a.dll" "$GAME/mono/lib/mono/4.5/"
done

echo "== managed: our engine + your game assemblies (never share this folder)"
cp "$B"/*.dll "$B/knl.exe" "$GAME/managed/"
rm -f "$GAME/managed/"*.stage1.dll
echo "== data: your device pack (never share)"
cp "$ROOT/pack/kingdom.knlpack" "$GAME/data/"
rm -rf "$GAME/data/music"; cp -r "$ROOT/pack/music" "$GAME/data/"

cp "$HERE/miyoo/knl.sh" "$GAME/"
cp "$HERE/miyoo/Kingdom New Lands.port" "$OUT/Roms/PORTS/Shortcuts/Strategy/"
echo "ok -> $OUT  (copy Roms/ to the SD card root)"

if [ $PUSH = 1 ]; then
  echo "== push to miyoo:/mnt/SDCARD"
  tar -C "$OUT" -cf - Roms | ssh miyoo "tar -C /mnt/SDCARD -xf -"
fi
