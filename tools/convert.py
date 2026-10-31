#!/usr/bin/env python3
"""
convert.py — Milestone 0 BYOD extractor for Kingdom New Lands.

Reads the user's own Kingdom_Data folder and writes an *inspection* format under
extracted/ (git-ignored, never redistributed):

  extracted/
    report.json              summary: counts, decode failures, texture colours, audio sizes
    objects/<file>.json      every serialized object as a type-tree dict (PPtrs kept as
                             {m_FileID, m_PathID}); MonoBehaviours decoded using type trees
                             rebuilt from the user's DLLs
    sprites.json             every Sprite: rect, pivot, ppu, atlas page + packing flags
    textures/<file>/<id>_<name>.png
    sprites/<file>/<id>.png  every Sprite cut out of its atlas page (mesh-masked)
    audio/sfx/<name>.wav     mono s16 PCM, resampled (default 22050 Hz)
    audio/music/<name>.ogg   Vorbis, resampled (default 22050 Hz stereo)
    fonts/<name>.ttf
    text/<name>.txt|.bytes

A later "pack" step turns this into the compact device format.

Usage:
    python tools/convert.py <path to Kingdom_Data> [--out extracted]
                            [--skip audio,textures,objects] [--rate 22050] [--music-q 2]
Tested with: Kingdom New Lands 1.2.8 (Unity 2017.1.2p1), UnityPy 1.25.x,
TypeTreeGeneratorAPI 0.0.10, ffmpeg 4.4 with libvorbis.
"""
import argparse
import collections
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time

import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

UNITY_VERSION = "2017.1.2p1"
SERIALIZED = ["globalgamemanagers", "globalgamemanagers.assets", "level0", "level1", "level2",
              "resources.assets", "sharedassets0.assets", "sharedassets1.assets",
              "sharedassets2.assets"]
# fields holding large binary payloads that are exported separately
STRIP = {"image data", "m_AudioData", "m_FontData", "m_Script_bytes"}


class PatchedTypeTreeGenerator(TypeTreeGenerator):
    """Fixes two TypeTreeGeneratorAPI 0.0.10 quirks for this Unity 2017.1 build.

    1) MonoBehaviour header alignment (see below).
    2) It emits `List<string>` / `string[]` fields as a node of type
    "string" whose child Array holds `string data` items. UnityPy then reads the list as a
    single string and runs off the end (seen on ScatteredObject.avoidOverlapWith). Retype
    such nodes to "vector" so they are read as arrays of strings."""

    def get_nodes(self, assembly, fullname):
        nodes = super().get_nodes(assembly, fullname)
        # MonoBehaviour header: the generator sets the 4-byte align flag on m_GameObject
        # instead of m_Enabled (UInt8), so m_Script is read 3 bytes early (m_FileID shows up
        # as 0x01000000). Unity 2017 aligns after m_Enabled.
        lvl1 = [n for n in nodes if n.m_Level == 1][:3]
        if [n.m_Name for n in lvl1] == ["m_GameObject", "m_Enabled", "m_Script"]:
            lvl1[0].m_MetaFlag = (lvl1[0].m_MetaFlag or 0) & ~0x4000
            lvl1[1].m_MetaFlag = (lvl1[1].m_MetaFlag or 0) | 0x4000
            lvl1[2].m_MetaFlag = (lvl1[2].m_MetaFlag or 0) & ~0x4000
        for i, n in enumerate(nodes):
            if n.m_Type != "string" or i + 2 >= len(nodes):
                continue
            arr, item = nodes[i + 1], nodes[i + 2]
            if (arr.m_Name == "Array" and arr.m_Level == n.m_Level + 1 and arr.m_Type == "Array"
                    and item.m_Level == n.m_Level + 2 and item.m_Type == "int"
                    and i + 3 < len(nodes) and nodes[i + 3].m_Level == n.m_Level + 2
                    and nodes[i + 3].m_Type == "string"):
                n.m_Type = "vector"
        return nodes


def safe(name):
    return re.sub(r"[^A-Za-z0-9._ -]+", "_", str(name)).strip() or "unnamed"


def jsonable(x):
    if isinstance(x, (bytes, bytearray, memoryview)):
        return {"$bytes": len(x)}
    if isinstance(x, dict):
        return {k: ({"$stripped": len(v)} if k in STRIP and hasattr(v, "__len__") else jsonable(v))
                for k, v in x.items()}
    if isinstance(x, (list, tuple)):
        return [jsonable(v) for v in x]
    if isinstance(x, float) and (x != x or x in (float("inf"), float("-inf"))):
        return str(x)
    return x


def ext_name(sf, file_id):
    """resolve a PPtr m_FileID to the target serialized file's basename (None if dangling)."""
    if file_id == 0:
        return os.path.basename(sf.name)
    if 0 < file_id <= len(sf.externals):
        return os.path.basename(sf.externals[file_id - 1].path)
    return None


def log(*a):
    print(time.strftime("[%H:%M:%S]"), *a, flush=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("data_dir")
    ap.add_argument("--out", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "extracted"))
    ap.add_argument("--skip", default="", help="comma list of: objects,textures,audio")
    ap.add_argument("--rate", type=int, default=22050, help="output sample rate for audio")
    ap.add_argument("--music-q", type=float, default=2.0, help="libvorbis quality for music")
    args = ap.parse_args()
    skip = set(filter(None, args.skip.split(",")))
    D = os.path.abspath(args.data_dir)
    OUT = os.path.abspath(args.out)
    os.makedirs(OUT, exist_ok=True)
    report = {"unity_version": UNITY_VERSION, "source": D, "files": {}, "type_counts": {},
              "monobehaviour_failures": {}, "textures": [], "audio": {"sfx": [], "music": []},
              "timings_s": {}}
    t0 = time.time()

    # -- type trees from the user's own DLLs (this build has none embedded) --
    gen = PatchedTypeTreeGenerator(UNITY_VERSION)
    managed = os.path.join(D, "Managed")
    for f in sorted(os.listdir(managed)):
        if f.endswith(".dll"):
            with open(os.path.join(managed, f), "rb") as fh:
                try:
                    gen.load_dll(fh.read())
                except Exception as e:
                    log("warn: could not load", f, e)

    paths = [os.path.join(D, f) for f in SERIALIZED if os.path.exists(os.path.join(D, f))]
    env = UnityPy.load(*paths)
    env.typetree_generator = gen
    log(f"loaded {len(env.files)} serialized files")

    # MonoScript lookup: (file name, path id) -> "Assembly:Namespace.Class"
    scripts = {}
    for sf_name, sf in list(env.files.items()):
        for o in getattr(sf, "objects", {}).values():
            if o.type.name == "MonoScript":
                d = o.read_typetree()
                ns = f"{d['m_Namespace']}." if d["m_Namespace"] else ""
                scripts[(os.path.basename(sf_name), o.path_id)] = f"{d['m_AssemblyName']}:{ns}{d['m_ClassName']}"

    def script_of(o, tree):
        ref = tree.get("m_Script") if isinstance(tree, dict) else None
        if not ref:
            return None
        fid, pid = ref["m_FileID"], ref["m_PathID"]
        target = ext_name(o.assets_file, fid)
        return scripts.get((target, pid), f"?{target}#{fid}:{pid}")

    type_counts = collections.Counter()
    mb_fail = collections.Counter()
    sprites = []
    tex_dir = os.path.join(OUT, "textures")

    # ---------------------------------------------------------------- objects
    t = time.time()
    for sf_name, sf in list(env.files.items()):
        if not hasattr(sf, "objects"):
            continue
        fname = os.path.basename(sf_name)
        objs = {}
        n_ok = n_bad = 0
        for pid, o in sf.objects.items():
            tname = o.type.name
            type_counts[tname] += 1
            entry = {"type": tname}
            try:
                tree = o.read_typetree()
                n_ok += 1
            except Exception as e:
                n_bad += 1
                tree = None
                entry["error"] = str(e)[:200]
                if tname == "MonoBehaviour":
                    # fall back to the raw header to at least learn which script failed
                    try:
                        raw = o.read(check_read=False)
                        s = raw.m_Script
                        tgt = ext_name(sf, s.m_FileID)
                        mb_fail[scripts.get((tgt, s.m_PathID), f"?{tgt}:{s.m_PathID}")] += 1
                    except Exception:
                        mb_fail["<unreadable header>"] += 1
            if tree is not None:
                if tname == "MonoBehaviour":
                    entry["script"] = script_of(o, tree)
                if "objects" not in skip:
                    entry["data"] = jsonable(tree)

                if tname == "Sprite":
                    rd = tree.get("m_RD", {})
                    raw = rd.get("settingsRaw", 0)
                    tex = rd.get("texture", {})
                    tex_file = ext_name(sf, tex.get("m_FileID", 0))
                    sprites.append({
                        "file": fname, "path_id": pid, "name": tree.get("m_Name"),
                        "rect": tree.get("m_Rect"), "pivot": tree.get("m_Pivot"),
                        "offset": tree.get("m_Offset"), "ppu": tree.get("m_PixelsToUnits"),
                        "texture": {"file": tex_file, "path_id": tex.get("m_PathID")},
                        "texture_rect": rd.get("textureRect"),
                        "texture_rect_offset": rd.get("textureRectOffset"),
                        "packed": bool(raw & 1), "packing_mode": "rectangle" if (raw >> 1) & 1 else "tight",
                        "rotation": (raw >> 2) & 0xF,
                        "atlas_tags": tree.get("m_AtlasTags"),
                    })
                    if "textures" not in skip:
                        # cut out (and mesh-mask, for tight packing) the sprite's pixels.
                        # image size == packed texture rect; img_offset = its bottom-left
                        # inside the sprite's full m_Rect, in pixels (y up, Unity convention)
                        try:
                            simg = o.read().image
                            sdir = os.path.join(OUT, "sprites", fname)
                            os.makedirs(sdir, exist_ok=True)
                            sp_path = os.path.join(sdir, f"{pid}.png")
                            simg.save(sp_path)
                            tro = rd.get("textureRectOffset") or {"x": 0, "y": 0}
                            sprites[-1]["img"] = os.path.relpath(sp_path, OUT)
                            sprites[-1]["img_size"] = [simg.width, simg.height]
                            sprites[-1]["img_offset"] = [int(round(tro["x"])), int(round(tro["y"]))]
                        except Exception as e:
                            sprites[-1]["img_error"] = str(e)[:200]

                if tname == "Texture2D" and "textures" not in skip:
                    try:
                        img = o.read().image
                        os.makedirs(os.path.join(tex_dir, fname), exist_ok=True)
                        p = os.path.join(tex_dir, fname, f"{pid}_{safe(tree['m_Name'])}.png")
                        img.save(p)
                        colours = img.convert("RGBA").getcolors(maxcolors=1 << 16)
                        report["textures"].append({
                            "file": fname, "path_id": pid, "name": tree["m_Name"],
                            "w": img.width, "h": img.height, "format": tree.get("m_TextureFormat"),
                            "rgba8888_bytes": img.width * img.height * 4,
                            "unique_rgba": len(colours) if colours else ">65536",
                            "unique_rgb": len({c[1][:3] for c in colours}) if colours else ">65536",
                            "png": os.path.relpath(p, OUT)})
                    except Exception as e:
                        report["textures"].append({"file": fname, "path_id": pid, "name": tree.get("m_Name"),
                                                   "error": str(e)[:200]})

                if tname == "Font":
                    data = tree.get("m_FontData")
                    if data:
                        os.makedirs(os.path.join(OUT, "fonts"), exist_ok=True)
                        with open(os.path.join(OUT, "fonts", f"{safe(tree['m_Name'])}_{pid}.ttf"), "wb") as fh:
                            fh.write(bytes(data))

                if tname == "TextAsset":
                    os.makedirs(os.path.join(OUT, "text"), exist_ok=True)
                    s = tree.get("m_Script", "")
                    b = s.encode("utf-8", "surrogateescape") if isinstance(s, str) else bytes(s)
                    with open(os.path.join(OUT, "text", f"{safe(tree['m_Name'])}_{pid}.bytes"), "wb") as fh:
                        fh.write(b)
            objs[pid] = entry

        if "objects" not in skip:
            os.makedirs(os.path.join(OUT, "objects"), exist_ok=True)
            with open(os.path.join(OUT, "objects", f"{fname}.json"), "w") as fh:
                json.dump({"file": fname,
                           "externals": [os.path.basename(e.path) for e in sf.externals],
                           "objects": objs}, fh, separators=(",", ":"))
        report["files"][fname] = {"objects": len(objs), "decoded": n_ok, "failed": n_bad}
        log(f"{fname}: {n_ok} decoded, {n_bad} failed")

    with open(os.path.join(OUT, "sprites.json"), "w") as fh:
        json.dump(sprites, fh, indent=0)
    report["type_counts"] = dict(type_counts.most_common())
    report["monobehaviour_failures"] = dict(mb_fail.most_common())
    report["sprites"] = {"count": len(sprites),
                         "packed": sum(s["packed"] for s in sprites),
                         "tight": sum(s["packed"] and s["packing_mode"] == "tight" for s in sprites),
                         "rotated": sum(s["rotation"] != 0 for s in sprites),
                         "ppu": dict(collections.Counter(s["ppu"] for s in sprites))}
    report["timings_s"]["objects_textures"] = round(time.time() - t, 1)

    # ------------------------------------------------------------------ audio
    if "audio" not in skip:
        t = time.time()
        if not shutil.which("ffmpeg"):
            sys.exit("ffmpeg not found (needed for audio). Re-run with --skip audio.")
        sfx_dir, mus_dir = os.path.join(OUT, "audio", "sfx"), os.path.join(OUT, "audio", "music")
        os.makedirs(sfx_dir, exist_ok=True)
        os.makedirs(mus_dir, exist_ok=True)
        # only clips owned by the game's serialized files, de-duplicated
        # fresh environment: the object pass leaves shared resource readers in a state that
        # makes FMOD reject the clip data (seen as FORMAT / INVALID PARAM errors)
        env_audio = UnityPy.load(*paths)
        clips, seen = [], set()
        for sf_name, sf in list(env_audio.files.items()):
            fname = os.path.basename(sf_name)
            if fname not in SERIALIZED or not hasattr(sf, "objects"):
                continue
            for pid, o in sf.objects.items():
                if o.type.name == "AudioClip" and (fname, pid) not in seen:
                    seen.add((fname, pid))
                    clips.append(o)
        log(f"audio: {len(clips)} clips")
        tmp = tempfile.mkdtemp(prefix="knl_audio_")
        try:
            for i, o in enumerate(clips, 1):
                c = o.read()
                name = safe(c.m_Name)
                # LoadType 2 = Streaming in Unity; long clips are music too
                is_music = c.m_LoadType == 2 or (getattr(c, "m_Length", 0) or 0) > 20
                entry = {"name": c.m_Name, "file": os.path.basename(o.assets_file.name), "path_id": o.path_id, "length_s": round(c.m_Length, 2),
                         "channels": c.m_Channels, "rate": c.m_Frequency,
                         "orig_bytes": c.m_Resource.m_Size if c.m_Resource else None,
                         "orig_format": c.m_CompressionFormat, "load_type": c.m_LoadType}
                try:
                    samples = c.samples  # {name.wav: bytes} decoded via FMOD
                    for k, wav in samples.items():
                        src = os.path.join(tmp, "in.wav")
                        with open(src, "wb") as fh:
                            fh.write(wav)
                        if is_music:
                            dst = os.path.join(mus_dir, f"{name}.ogg")
                            cmd = ["ffmpeg", "-v", "error", "-y", "-i", src, "-ar", str(args.rate),
                                   "-c:a", "libvorbis", "-q:a", str(args.music_q), dst]
                        else:
                            dst = os.path.join(sfx_dir, f"{name}.wav")
                            cmd = ["ffmpeg", "-v", "error", "-y", "-i", src, "-ac", "1",
                                   "-ar", str(args.rate), "-c:a", "pcm_s16le", dst]
                        subprocess.run(cmd, check=True)
                        entry["out"] = os.path.relpath(dst, OUT)
                        entry["out_bytes"] = os.path.getsize(dst)
                        break
                except Exception as e:
                    entry["error"] = str(e)[:200]
                report["audio"]["music" if is_music else "sfx"].append(entry)
                if i % 50 == 0 or i == len(clips):
                    log(f"audio {i}/{len(clips)}")
        finally:
            shutil.rmtree(tmp, ignore_errors=True)
        a = report["audio"]
        a["summary"] = {k: {"count": len(a[k]),
                            "out_bytes": sum(e.get("out_bytes", 0) for e in a[k]),
                            "errors": sum("error" in e for e in a[k])} for k in ("sfx", "music")}
        report["timings_s"]["audio"] = round(time.time() - t, 1)

    # ---------------------------------------------------------------- summary
    tx = [x for x in report["textures"] if "w" in x]
    report["textures_summary"] = {
        "count": len(tx), "rgba8888_bytes": sum(x["rgba8888_bytes"] for x in tx),
        "fits_8bit_indexed": [x["name"] for x in tx if isinstance(x["unique_rgba"], int) and x["unique_rgba"] <= 256],
        "over_256_colours": [(x["name"], x["unique_rgba"]) for x in tx
                             if not (isinstance(x["unique_rgba"], int) and x["unique_rgba"] <= 256)],
    }
    report["timings_s"]["total"] = round(time.time() - t0, 1)
    with open(os.path.join(OUT, "report.json"), "w") as fh:
        json.dump(report, fh, indent=1, default=str)
    log(f"done in {report['timings_s']['total']} s -> {OUT}/report.json")


if __name__ == "__main__":
    main()
