#!/usr/bin/env python3
"""Create a deterministic BYOD archive; never walk the developer's game-data tree."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
GAME = "Roms/PORTS/Games/KingdomNewLands/"
RUNTIME = ["knl.sh", "lib/libknlcore.so", "lib/libgssapi_krb5.so.2", "mono/bin/mono-sgen", "mono/COPYRIGHT",
           "mono/lib/libmono-native.so", "mono/etc/mono/config"]
RUNTIME += ["managed/" + n for n in ("UnityEngine.dll", "Rewired_Core.dll", "knl.dll")]
RUNTIME += ["mono/lib/mono/4.5/" + n + ".dll" for n in
            ("mscorlib", "System", "System.Core", "System.Xml", "System.Configuration", "Mono.Security")]
PLAYER = ["prepare_data.py", "setup_windows.bat", "setup.sh", "setup_gui.py", "README.md", "VERSION", "THIRD_PARTY_NOTICES.md", "docs/TESTING.md",
          "tools/convert.py", "tools/pack.py", "tools/data_checks.py", "tools/supported_build.json", "tools/requirements.txt"]


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--build", type=Path, default=ROOT / "engine/build-miyoo")
    ap.add_argument("--out", type=Path, default=ROOT / "dist")
    args = ap.parse_args()
    version = (ROOT / "VERSION").read_text().strip()
    payload = {}
    for name in RUNTIME:
        payload[GAME + name] = (args.build / GAME / name).read_bytes()
    shortcut = "Roms/PORTS/Shortcuts/Strategy/Kingdom New Lands.port"
    payload[shortcut] = (ROOT / "engine/miyoo/Kingdom New Lands.port").read_bytes()
    for name in PLAYER:
        payload[name] = (ROOT / name).read_bytes()
    payload[GAME + "data/PLACE_DATA_HERE.txt"] = b"Run the included setup_windows.bat or setup.sh to prepare your owned game data. See README.md.\n"
    commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    dirty = bool(subprocess.check_output(["git", "status", "--porcelain", "--untracked-files=no"], cwd=ROOT))
    manifest = {"version": version, "source_commit": commit, "source_modified": dirty,
                "status": "experimental; full gameplay validation pending",
                "files": {n: hashlib.sha256(b).hexdigest() for n, b in sorted(payload.items())}}
    payload["release-manifest.json"] = (json.dumps(manifest, indent=2) + "\n").encode()
    args.out.mkdir(parents=True, exist_ok=True)
    destination = args.out / ("KingdomNewLands-MiyooMini-" + version + ".zip")
    temporary = destination.with_suffix(".zip.tmp")
    try:
        with zipfile.ZipFile(temporary, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for name, data in sorted(payload.items()):
                info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
                info.create_system = 3
                mode = 0o755 if name.endswith((".sh", ".port", "mono-sgen")) else 0o644
                info.external_attr = (0o100000 | mode) << 16
                archive.writestr(info, data, compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)
        with zipfile.ZipFile(temporary) as archive:
            if archive.testzip():
                raise ValueError("Archive verification failed")
        temporary.replace(destination)
    finally:
        temporary.unlink(missing_ok=True)
    sha = hashlib.sha256(destination.read_bytes()).hexdigest()
    destination.with_suffix(".zip.sha256").write_text(sha + "  " + destination.name + "\n")
    print("Candidate: " + str(destination))
    print("SHA256: " + sha)
    if dirty:
        print("Source tree has uncommitted changes; commit before publishing.")


if __name__ == "__main__":
    main()
