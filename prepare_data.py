#!/usr/bin/env python3
"""Prepare owned Kingdom data and install a validated candidate without compiling."""
import argparse
import datetime
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import traceback

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE / "tools"))
from data_checks import GAME_DLLS, digest, validate_extraction, validate_pack, validate_source


def find_kingdom_data(path_str=None):
    if path_str:
        candidates = [Path(path_str).expanduser()]
    else:
        candidates = [
            HERE / "Kingdom_Data", Path.home() / "Games/KingdomNewLands/Kingdom_Data",
            Path.home() / "Games/Heroic/KingdomNewLands/Kingdom_Data",
            Path.home() / ".local/share/Steam/steamapps/common/Kingdom New Lands/Kingdom_Data",
            Path.home() / ".steam/steam/steamapps/common/Kingdom New Lands/Kingdom_Data",
            Path.home() / "GOG Games/Kingdom New Lands/Kingdom_Data",
            Path("C:/Program Files (x86)/Steam/steamapps/common/Kingdom New Lands/Kingdom_Data"),
            Path("C:/GOG Games/Kingdom New Lands/Kingdom_Data"),
            Path("C:/Program Files/Epic Games/KingdomNewLands/Kingdom_Data"),
        ]
    for p in candidates:
        if (p / "Kingdom_Data").is_dir():
            p = p / "Kingdom_Data"
        if p.is_dir() and p.name == "Kingdom_Data":
            return p.resolve()
    return None  # An explicitly invalid path must not fall back to a different installation.


def check_dependencies():
    missing = []
    for pkg in ("UnityPy", "PIL", "dnfile", "TypeTreeGeneratorAPI"):
        try:
            __import__(pkg)
        except ImportError:
            missing.append(pkg)
    if missing:
        raise ValueError("Missing Python dependencies: " + ", ".join(missing) +
                         ". Run setup_windows.bat or setup.sh, or install tools/requirements.txt in a virtual environment.")


def verify_bundle():
    manifest = HERE / "release-manifest.json"
    if not manifest.exists():
        return
    for name, expected in json.loads(manifest.read_text(encoding="utf-8"))["files"].items():
        path = (HERE / name).resolve()
        if HERE not in path.parents or not path.is_file() or digest(path) != expected:
            raise ValueError("Release file is missing or modified: " + name + ". Extract a fresh complete ZIP.")


def run_step(command):
    # A tee through a pipe keeps child output in the preparation log as well as the console.
    with subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                          text=True, errors="replace", encoding="utf-8") as process:
        for line in process.stdout:
            print(line.rstrip(), flush=True)
        if process.wait():
            raise RuntimeError("Preparation step failed: " + Path(command[1]).name)


def install_candidate(pack_dir, data_dir, out_root, manifest):
    target = out_root / "Roms/PORTS/Games/KingdomNewLands"
    target.parent.mkdir(parents=True, exist_ok=True)
    bundle = HERE / "Roms/PORTS/Games/KingdomNewLands"
    if not target.is_dir() and not bundle.is_dir():
        raise ValueError("No port runtime found. Use the complete release ZIP, or extract its Roms folder to this output first.")
    if (target / ".install-journal").exists():
        raise ValueError("An on-device installation was interrupted. Launch the port on the Miyoo to recover it before using PC setup.")
    # Whole-directory replacement on the destination filesystem. The previous installation,
    # including its saves, is retained as a backup; a failed rename rolls back.
    with tempfile.TemporaryDirectory(prefix=".knl-install-", dir=str(target.parent)) as temporary:
        stage = Path(temporary) / "KingdomNewLands"
        if target.exists():
            # The previous directory remains intact as the rollback copy. Its native
            # receipt/checkpoints describe different data and must not follow this pack.
            native_state = {".native-ready", ".prepare-v1", ".install-rollback", ".prepare.lock"}
            shutil.copytree(target, stage, ignore=lambda directory, names:
                            native_state.intersection(names) if Path(directory) == target else set())
        else:
            stage.mkdir()
        if bundle.exists():
            shutil.copytree(bundle, stage, dirs_exist_ok=True)
        for name in ("data", "managed"):
            folder = stage / name
            if name == "managed":
                keep = {n: (folder / n).read_bytes() for n in ("UnityEngine.dll", "Rewired_Core.dll", "knl.dll", "knl.exe")
                        if (folder / n).is_file()}
            if folder.exists():
                shutil.rmtree(folder)
            folder.mkdir()
            if name == "managed":
                for n, content in keep.items():
                    (folder / n).write_bytes(content)
        # Prefer the current host over a leftover pre-1.1 executable.
        if (stage / "managed/knl.dll").exists():
            (stage / "managed/knl.exe").unlink(missing_ok=True)
        shutil.copy2(pack_dir / "kingdom.knlpack", stage / "data/kingdom.knlpack")
        shutil.copytree(pack_dir / "music", stage / "data/music")
        for name in GAME_DLLS:
            shutil.copy2(data_dir / "Managed" / name, stage / "managed" / name)
            if digest(data_dir / "Managed" / name) != digest(stage / "managed" / name):
                raise IOError("Copy verification failed: " + name)
        if digest(pack_dir / "kingdom.knlpack") != digest(stage / "data/kingdom.knlpack"):
            raise IOError("Pack copy verification failed")
        (stage / "save").mkdir(exist_ok=True)
        (stage / "logs").mkdir(exist_ok=True)
        for name in ("knl.sh", "managed/UnityEngine.dll", "managed/Rewired_Core.dll", "mono/bin/mono-sgen", "lib/libknlcore.so"):
            if not (stage / name).is_file():
                raise ValueError("Port runtime is incomplete: " + name)
        if not any((stage / "managed" / name).is_file() for name in ("knl.dll", "knl.exe")):
            raise ValueError("Port host is missing")
        validate_pack(stage / "data/kingdom.knlpack")
        manifest["installed_files"] = {p.relative_to(stage).as_posix(): digest(p)
                                       for sub in ("data", "managed") for p in sorted((stage / sub).rglob("*")) if p.is_file()}
        (stage / "installation.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        shortcuts = HERE / "Roms/PORTS/Shortcuts"
        if shortcuts.exists():
            shutil.copytree(shortcuts, out_root / "Roms/PORTS/Shortcuts", dirs_exist_ok=True)
        backup = target.with_name("KingdomNewLands.backup-" + datetime.datetime.now().strftime("%Y%m%d-%H%M%S-%f"))
        had_target = target.exists()
        if had_target:
            target.rename(backup)
        try:
            stage.rename(target)
        except BaseException:
            if had_target:
                backup.rename(target)
            raise
        if had_target:
            print("Previous installation and saves retained at: " + str(backup))
    return target


def prepare(args):
    verify_bundle()
    data_dir = find_kingdom_data(args.data_dir)
    if data_dir is None:
        raise ValueError("Kingdom_Data was not found at the supplied location.")
    source = validate_source(data_dir, args.allow_unsupported)
    print("Source profile: " + source["profile"], flush=True)
    check_dependencies()
    skip = {s.strip() for s in args.skip.split(",") if s.strip()}
    if args.skip_audio:
        skip.add("audio")
    if skip - {"audio"}:
        raise ValueError("A playable installation requires objects and textures. Only --skip-audio is supported.")
    if "audio" not in skip and not shutil.which("ffmpeg"):
        raise ValueError("FFmpeg was not found. Install it, or explicitly select --skip-audio for a silent test installation.")
    if "audio" in skip:
        print("WARNING: creating a silent test installation; audio is disabled.", flush=True)
    out_root = Path(args.out).expanduser().resolve()
    out_root.mkdir(parents=True, exist_ok=True)
    # Conversion takes about 1 GB of temporary disk space with the verified input.
    if shutil.disk_usage(tempfile.gettempdir()).free < 2 * 1024**3:
        raise ValueError("At least 2 GiB of free temporary disk space is required.")
    with tempfile.TemporaryDirectory(prefix="knl-prepare-") as temporary:
        work = Path(temporary)
        extracted = work / "extracted"
        if args.skip_extract:
            extracted = HERE / "extracted"
            previous = json.loads((extracted / "report.json").read_text(encoding="utf-8"))
            if previous.get("source_manifest") != source:
                raise ValueError("Cached extraction does not match this source. Run again without --skip-extract.")
        else:
            command = [sys.executable, str(HERE / "tools/convert.py"), str(data_dir), "--out", str(extracted)]
            if args.allow_unsupported:
                command.append("--allow-unsupported")
            if skip:
                command.extend(["--skip", ",".join(sorted(skip))])
            print("Step 1/3: extracting owned game data", flush=True)
            run_step(command)
        validate_extraction(extracted, audio="audio" not in skip)
        pack = work / "pack"
        print("Step 2/3: packing and validating game data", flush=True)
        command = [sys.executable, str(HERE / "tools/pack.py"), str(extracted), "--out", str(pack)]
        if "audio" in skip:
            command.append("--skip-audio")
        run_step(command)
        validate_pack(pack / "kingdom.knlpack")
        needed = sum(p.stat().st_size for p in pack.rglob("*") if p.is_file())
        old = out_root / "Roms/PORTS/Games/KingdomNewLands"
        needed += sum(p.stat().st_size for p in old.rglob("*") if p.is_file()) if old.exists() else 32 * 1024**2
        if shutil.disk_usage(out_root).free < needed + 64 * 1024**2:
            raise ValueError("Not enough free space for a staged installation and backup.")
        print("Step 3/3: installing validated data", flush=True)
        target = install_candidate(pack, data_dir, out_root,
                                   {"source": source, "audio": "audio" not in skip,
                                    "prepared_utc": datetime.datetime.now(datetime.timezone.utc).isoformat()})
        print("DATA PREPARATION COMPLETE: " + str(target), flush=True)
        print("This is an experimental release candidate. Keep the backup until gameplay and save/reload testing pass.")


class Tee:
    def __init__(self, console, log):
        self.console, self.log = console, log
    def write(self, text):
        self.console.write(text)
        self.log.write(text)
    def flush(self):
        self.console.flush()
        self.log.flush()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("data_dir", nargs="?")
    parser.add_argument("--out", "-o", default=str(HERE / "sd_card_data"))
    parser.add_argument("--skip-extract", action="store_true", help="Reuse a fingerprint-matched developer extraction")
    parser.add_argument("--skip-audio", action="store_true")
    parser.add_argument("--skip", default="", help="Only audio may be skipped")
    parser.add_argument("--allow-unsupported", action="store_true", help="Opt in to an unverified game build for testing")
    parser.add_argument("--log", default=str(HERE / "prepare.log"))
    args = parser.parse_args()
    original = sys.stdout
    with Path(args.log).expanduser().open("w", encoding="utf-8") as log:
        sys.stdout = Tee(original, log)
        try:
            prepare(args)
            return 0
        except Exception as error:
            print("PREPARATION FAILED: " + str(error), flush=True)
            traceback.print_exc(file=log)
            return 1
        finally:
            sys.stdout = original
            print("Preparation log: " + str(Path(args.log).resolve()))


if __name__ == "__main__":
    sys.exit(main())
