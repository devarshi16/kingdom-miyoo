#!/usr/bin/env python3
"""Recover only discarded UI.Text layout metadata for an existing local pack.

Uses the verified user's original files, writes a hash-bound JSON sidecar, and never edits
the pack, runtime, or saves. New conversions keep this metadata inline and need no sidecar.
"""
import argparse
import hashlib
import json
from pathlib import Path

from data_checks import SERIALIZED, validate_pack, validate_source


def recover(source, data_dir):
    from convert import PatchedTypeTreeGenerator, UNITY_VERSION, jsonable
    import UnityPy
    source, data_dir = Path(source), Path(data_dir)
    manifest = validate_source(source)
    pack_path = data_dir / "kingdom.knlpack"
    entries = validate_pack(pack_path)
    generator = PatchedTypeTreeGenerator(UNITY_VERSION)
    for path in sorted((source / "Managed").glob("*.dll")):
        generator.load_dll(path.read_bytes())
    env = UnityPy.load(*(str(source / name) for name in SERIALIZED))
    env.typetree_generator = generator
    assets = {}
    for obj in env.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        tree = obj.read_typetree()
        font = tree.get("m_FontData")
        if not isinstance(font, dict) or "m_Font" not in font or "m_FontSize" not in font:
            continue
        name = Path(obj.assets_file.name).name
        asset = assets.setdefault(name, {"fields": {}})
        asset["fields"][str(obj.path_id)] = jsonable(font)
    if not assets:
        raise ValueError("No UI.Text font layout metadata was recovered")
    with pack_path.open("rb") as stream:
        for name, asset in assets.items():
            offset, length = entries["objects/" + name]
            stream.seek(offset)
            asset["entry_sha256"] = hashlib.sha256(stream.read(length)).hexdigest()
    return {"format": 1, "source_profile": manifest["profile"],
            "source_files": manifest["files"], "assets": assets}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="Verified original Kingdom_Data directory")
    parser.add_argument("--data-dir", required=True, type=Path, help="Existing local data directory containing kingdom.knlpack")
    parser.add_argument("--out", type=Path, help="Default: DATA_DIR/ui-fontdata.json")
    args = parser.parse_args()
    out = args.out or args.data_dir / "ui-fontdata.json"
    if out.exists():
        parser.error("Output already exists; choose a new --out path to preserve prior metadata")
    result = recover(args.source, args.data_dir)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(result, separators=(",", ":")) + "\n", encoding="utf-8")
    print("Recovered", sum(len(a["fields"]) for a in result["assets"].values()), "UI.Text layouts ->", out)


if __name__ == "__main__":
    main()
