#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/../../.." && pwd)"
runtime="${KNL_CATAPULT_RUNTIME_DIR:-$root/engine/build}"
data="${KNL_CATAPULT_DATA_DIR:-$root/pack}"
save_source="${KNL_CATAPULT_SAVE_DIR:?Set KNL_CATAPULT_SAVE_DIR to a save directory to copy for this experiment}"
export KNL_CATAPULT_LAST_FRAME="${KNL_CATAPULT_LAST_FRAME:-4200}"
[[ "$KNL_CATAPULT_LAST_FRAME" =~ ^[0-9]+$ ]] && (( KNL_CATAPULT_LAST_FRAME >= 1000 && KNL_CATAPULT_LAST_FRAME <= 20000 )) || { echo "Invalid final frame" >&2; exit 2; }
evidence="${KNL_CATAPULT_PROBE_OUT:-$root/docs/evidence/catapult-natural-placement-port.json}"
probe_tmp="$(mktemp -d)"
trap 'rm -rf "$probe_tmp"' EXIT
test -f "$runtime/UnityEngine.dll" || { echo "missing private runtime: $runtime/UnityEngine.dll" >&2; exit 2; }
test -f "$runtime/libknlcore.so" || { echo "missing private runtime: $runtime/libknlcore.so" >&2; exit 2; }
test -d "$data" || { echo "missing game data: $data" >&2; exit 2; }
test -d "$save_source" || { echo "missing save directory: $save_source" >&2; exit 2; }
mkdir -p "$(dirname "$evidence")"
cp -a "$save_source" "$probe_tmp/save"
save_sha256="$(python3 - "$save_source" <<'PY'
import hashlib, os, sys
root = os.path.abspath(sys.argv[1])
h = hashlib.sha256()
for base, dirs, files in os.walk(root):
    dirs.sort()
    for name in sorted(files):
        path = os.path.join(base, name)
        rel = os.path.relpath(path, root).replace(os.sep, "/")
        h.update(rel.encode("utf-8")); h.update(b"\0")
        with open(path, "rb") as f:
            for block in iter(lambda: f.read(1024 * 1024), b""):
                h.update(block)
        h.update(b"\0")
print(h.hexdigest())
PY
)"
python3 - "$root/engine/Host/Program.cs" "$root/tools/probes/catapult_natural_placement/probe.csfrag" "$probe_tmp/Program.cs" <<'PY'
import pathlib, sys
host = pathlib.Path(sys.argv[1]).read_text()
probe = pathlib.Path(sys.argv[2]).read_text()
main_marker = "    static int Main(string[] argv)\n"
frame_marker = "            RenderSystem.RenderFrame();\n            Coroutines.TickEndOfFrame();\n            renderMs += sw.Elapsed.TotalMilliseconds - t1; logicMs += t1 - t0;\n"
if host.count(main_marker) != 1 or host.count(frame_marker) != 1:
    raise SystemExit("Host Program.cs changed; update the probe injection anchors before running")
host = host.replace(main_marker, probe + "\n" + main_marker, 1)
host = host.replace(frame_marker, "            RenderSystem.RenderFrame();\n            CatapultNaturalProbeFrame(f);\n            Coroutines.TickEndOfFrame();\n            renderMs += sw.Elapsed.TotalMilliseconds - t1; logicMs += t1 - t0;\n", 1)
pathlib.Path(sys.argv[3]).write_text(host)
PY
mcs -optimize+ -target:exe -out:"$probe_tmp/knl.dll" -r:"$runtime/UnityEngine.dll" -r:System.Core.dll "$probe_tmp/Program.cs"
export MONO_PATH="$probe_tmp:$runtime"
export LD_LIBRARY_PATH="$runtime${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
export KNL_CATAPULT_PROBE_OUT="$evidence"
export KNL_CATAPULT_SAVE_SHA256="$save_sha256"
mono "$probe_tmp/knl.dll" --data "$data" --save "$probe_tmp/save" --boot --frames "$((KNL_CATAPULT_LAST_FRAME+1))" --fixed-dt --quiet
echo "save_sha256=$save_sha256"
echo "evidence=$evidence"
