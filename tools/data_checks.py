"""Validation shared by the player preparer and developer conversion tools."""
import hashlib
import json
import struct
from pathlib import Path

SERIALIZED = (
    "globalgamemanagers", "globalgamemanagers.assets", "level0", "level1", "level2",
    "resources.assets", "sharedassets0.assets", "sharedassets1.assets", "sharedassets2.assets",
)
GAME_DLLS = (
    "Assembly-CSharp.dll", "Assembly-CSharp-firstpass.dll", "UnityEngine.UI.dll",
    "UnityEngine.Timeline.dll", "UnityEngine.Networking.dll", "DOTween.dll",
    "DOTween43.dll", "DOTween46.dll", "DOTween50.dll", "DOTweenPro.dll", "DemiLib.dll",
    "UnityEngine.Analytics.dll", "XInputDotNetPure.dll", "SonyNP.dll",
    "SonyPS4CommonDialog.dll", "SonyPS4SavedGames.dll",
)


def digest(path):
    h = hashlib.sha256()
    with Path(path).open("rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def validate_source(data, allow_unsupported=False):
    data = Path(data)
    required = list(SERIALIZED) + ["Managed/" + n for n in GAME_DLLS]
    required += ["Managed/UnityEngine.dll", "Managed/Rewired_Core.dll"]
    missing = [n for n in required if not (data / n).is_file() or not (data / n).stat().st_size]
    if missing:
        raise ValueError("Incomplete Kingdom_Data. Missing/empty files: " + ", ".join(missing))
    profile = json.loads(Path(__file__).with_name("supported_build.json").read_text(encoding="utf-8"))
    matched = all(digest(data / n) == sha for n, sha in profile["fingerprints"].items())
    if not matched and not allow_unsupported:
        raise ValueError("This game build has not been verified. The tested profile is " + profile["name"] +
                         ". Use --allow-unsupported only to create an explicitly untested candidate.")
    # Resource streams are hashed too: cached extraction must never mix different source data.
    source_files = sorted(set(required + [p.name for p in data.iterdir()
                                         if p.is_file() and p.suffix in (".resource", ".resS")]))
    return {"profile": profile["name"] if matched else "UNVERIFIED",
            "files": {n: digest(data / n) for n in source_files}}


def validate_extraction(directory, audio=True):
    directory = Path(directory)
    report = json.loads((directory / "report.json").read_text(encoding="utf-8"))
    for name in SERIALIZED:
        info = report.get("files", {}).get(name, {})
        if not info.get("decoded") or info.get("failed", 0):
            raise ValueError("Incomplete/failed object extraction: " + name)
        if not (directory / "objects" / (name + ".json")).is_file():
            raise ValueError("Missing extracted objects: " + name)
    sprites = json.loads((directory / "sprites.json").read_text(encoding="utf-8"))
    if not sprites or any("img" not in s or not (directory / s["img"]).is_file() for s in sprites):
        raise ValueError("Missing or failed sprite extraction")
    if any("error" in t for t in report.get("textures", [])):
        raise ValueError("Texture extraction failed; inspect the preparation log")
    if audio:
        for kind in ("sfx", "music"):
            clips = report.get("audio", {}).get(kind, [])
            if not clips or any("out" not in c or not (directory / c["out"]).is_file() for c in clips):
                raise ValueError("Missing or failed audio extraction: " + kind)
    return report


def validate_pack(path):
    """Reject empty, truncated, overlapping and out-of-bounds archives before installation."""
    path = Path(path)
    size = path.stat().st_size
    with path.open("rb") as f:
        header = f.read(12)
        if len(header) != 12 or header[:4] != b"KNLP":
            raise ValueError("Invalid pack header")
        version, count = struct.unpack("<II", header[4:])
        if version != 1 or not 1 <= count <= 100000:
            raise ValueError("Unsupported or empty pack")
        entries = {}
        for _ in range(count):
            raw = f.read(2)
            if len(raw) != 2:
                raise ValueError("Truncated pack directory")
            n = struct.unpack("<H", raw)[0]
            if not 1 <= n <= 4096:
                raise ValueError("Invalid pack entry name")
            name = f.read(n).decode("utf-8")
            raw = f.read(12)
            if len(raw) != 12 or name in entries:
                raise ValueError("Truncated or duplicate pack entry")
            offset, length = struct.unpack("<QI", raw)
            if not length or offset + length > size:
                raise ValueError("Pack entry outside archive: " + name)
            entries[name] = (offset, length)
        end = f.tell()
        for name, (offset, length) in sorted(entries.items(), key=lambda e: e[1][0]):
            if offset < end:
                raise ValueError("Overlapping pack entry: " + name)
            end = offset + length
        for name in ["objects/" + n for n in SERIALIZED] + ["sprites"]:
            if name not in entries:
                raise ValueError("Pack missing required entry: " + name)
        f.seek(entries["sprites"][0])
        raw = f.read(12)
        if len(raw) != 12 or raw[:4] != b"KNLS" or struct.unpack("<I", raw[8:])[0] == 0:
            raise ValueError("Pack contains no sprites")
    return entries
