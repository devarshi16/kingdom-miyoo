#!/usr/bin/env python3
"""
pack.py - build the device data pack from convert.py's output (Milestone 3, D19).

    tools/run.sh pack.py [extracted/] [--out pack/]

Writes
    pack/kingdom.knlpack   one archive: objects (binary, lazily decoded), sprites (8-bit indexed,
                           one RGBA palette per sprite), UI textures, text assets, SFX (IMA ADPCM)
    pack/music/*.ogg       Vorbis music and long loops, streamed from disk (copied as-is)

Everything in the pack is derived from YOUR game data: never share it.
"""
import argparse, json, os, struct, sys, time, wave, array, glob, shutil
from PIL import Image
from data_checks import validate_extraction, validate_pack

T0 = time.time()
def log(*a):
    print(f"[{time.time() - T0:6.1f}s]", *a, flush=True)

ap = argparse.ArgumentParser()
ap.add_argument("extracted", nargs="?", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "extracted"))
ap.add_argument("--out", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "pack"))
ap.add_argument("--skip-audio", action="store_true")
args = ap.parse_args()
src = os.path.abspath(args.extracted)
out = os.path.abspath(args.out)
validate_extraction(src, audio=not args.skip_audio)
if os.path.exists(out) and os.listdir(out):
    sys.exit("Pack output must be empty; select a new --out directory.")
os.makedirs(os.path.join(out, "music"), exist_ok=True)
entries = []   # (name, bytes)

# ---------------------------------------------------------------- helpers
def varint(n):
    b = bytearray()
    while True:
        x = n & 0x7F; n >>= 7
        if n: b.append(x | 0x80)
        else: b.append(x); return bytes(b)

def zz(n):            # zigzag for signed ints
    return (n << 1) ^ (n >> 63)

# ---------------------------------------------------------------- objects
# value tags: 0 null, 1 false, 2 true, 3 int (zigzag varint), 4 float64, 5 string (table id),
#             6 list, 7 dict (count, then key id + value), 8 float32
for path in sorted(glob.glob(os.path.join(src, "objects", "*.json"))):
    fname = os.path.basename(path)[:-5]
    root = json.load(open(path, encoding="utf-8"))
    strings, sid = [], {}
    def S(s):
        i = sid.get(s)
        if i is None:
            i = sid[s] = len(strings); strings.append(s)
        return i
    blob = bytearray()
    def enc(v):
        if v is None: blob.append(0)
        elif v is True: blob.append(2)
        elif v is False: blob.append(1)
        elif isinstance(v, int) and -(1 << 62) < v < (1 << 62): blob.append(3); blob.extend(varint(zz(v)))
        elif isinstance(v, (int, float)):
            f = float(v)
            if struct.unpack("<f", struct.pack("<f", f))[0] == f: blob.append(8); blob.extend(struct.pack("<f", f))
            else: blob.append(4); blob.extend(struct.pack("<d", f))
        elif isinstance(v, str): blob.append(5); blob.extend(varint(S(v)))
        elif isinstance(v, list):
            blob.append(6); blob.extend(varint(len(v)))
            for x in v: enc(x)
        elif isinstance(v, dict):
            blob.append(7); blob.extend(varint(len(v)))
            for k, x in v.items(): blob.extend(varint(S(k))); enc(x)
        else: raise TypeError(type(v))
    index = []
    for pid, o in root["objects"].items():
        start = len(blob)
        enc(o.get("data"))
        index.append((int(pid), S(o.get("type") or ""), S(o.get("script") or ""), start, len(blob) - start))
    ext = [S(e) for e in root.get("externals", [])]
    b = bytearray(b"KNLB")
    b += struct.pack("<I", 1)
    b += struct.pack("<I", len(strings))
    for s in strings:
        u = s.encode("utf-8"); b += varint(len(u)); b += u
    b += struct.pack("<I", len(ext)) + b"".join(struct.pack("<I", e) for e in ext)
    b += struct.pack("<I", len(index))
    for pid, t, sc, off, ln in index:
        b += struct.pack("<qIIII", pid, t, sc, off, ln)
    b += blob
    entries.append(("objects/" + fname, bytes(b)))
    log(f"objects {fname}: {len(index)} objects, {os.path.getsize(path) / 1e6:.1f} MB json -> {len(b) / 1e6:.2f} MB")

# ---------------------------------------------------------------- sprites
# record: file sid, pid, name, rect x y w h, pivot x y, ppu, imgW imgH offX offY, fmt (0 idx8 / 1 rgba),
#         palette (count + RGBA), pixels
sprites = json.load(open(os.path.join(src, "sprites.json"), encoding="utf-8"))
sb = bytearray(b"KNLS") + struct.pack("<II", 1, 0)
nspr = idx8 = rgba = 0
def wstr(buf, s):
    u = s.encode("utf-8"); buf += varint(len(u)); buf += u
for e in sprites:
    if "img" not in e: continue
    p = os.path.join(src, e["img"])
    if not os.path.exists(p): continue
    im = Image.open(p).convert("RGBA")
    w, h = im.size
    px = list(im.get_flattened_data()) if hasattr(im, 'get_flattened_data') else list(im.getdata())
    pal = sorted(set(px))
    wstr(sb, e["file"]); sb += struct.pack("<q", int(e["path_id"])); wstr(sb, e.get("name", ""))
    r = e["rect"]; pv = e["pivot"]
    sb += struct.pack("<4f2ff4i", r["x"], r["y"], r["width"], r["height"], pv["x"], pv["y"], float(e.get("ppu", 32.0)),
                      w, h, int(e["img_offset"][0]), int(e["img_offset"][1]))
    if len(pal) <= 256:
        lut = {c: i for i, c in enumerate(pal)}
        sb.append(0); sb += struct.pack("<H", len(pal))
        for c in pal: sb += bytes(c)
        sb += bytes(lut[c] for c in px)
        idx8 += 1
    else:
        sb.append(1); sb += struct.pack("<H", 0)
        sb += im.tobytes()
        rgba += 1
    nspr += 1
struct.pack_into("<I", sb, 8, nspr)
entries.append(("sprites", bytes(sb)))
log(f"sprites: {nspr} ({idx8} indexed, {rgba} rgba) -> {len(sb) / 1e6:.2f} MB")

# ---------------------------------------------------------------- textures (UI fonts, raw images)
nt = 0
for p in sorted(glob.glob(os.path.join(src, "textures", "*", "*.png"))):
    fdir = os.path.basename(os.path.dirname(p)); pid = os.path.basename(p).split("_", 1)[0]
    if "SpriteAtlasTexture" in p: continue       # sprites are drawn from their own cut-outs
    im = Image.open(p).convert("RGBA")
    entries.append((f"textures/{fdir}/{pid}", struct.pack("<II", *im.size) + im.tobytes()))
    nt += 1
log(f"textures: {nt}")

# ---------------------------------------------------------------- text assets
nx = 0
for p in sorted(glob.glob(os.path.join(src, "text", "**", "*"), recursive=True)):
    if os.path.isfile(p):
        entries.append(("text/" + os.path.relpath(p, os.path.join(src, "text")).replace(os.sep, "/"), open(p, "rb").read())); nx += 1
log(f"text assets: {nx}")

# ---------------------------------------------------------------- audio
# SFX: IMA ADPCM, mono, blocks of 1024 samples: int16 predictor, uint8 step index, pad, 512 bytes nibbles
STEP = [7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118,
        130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060,
        1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484,
        7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767]
IDX = [-1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8]
BLOCK = 1024

def adpcm(samples):
    outb = bytearray()
    for b0 in range(0, len(samples), BLOCK):
        blk = samples[b0:b0 + BLOCK]
        blk = blk + [blk[-1] if blk else 0] * (BLOCK - len(blk))
        pred, index = blk[0], 0
        # pick a starting step index from the first delta
        d0 = abs(blk[1] - blk[0]) if len(blk) > 1 else 0
        while index < 88 and STEP[index] < d0: index += 1
        outb += struct.pack("<hBB", pred, index, 0)
        nib = []
        for s in blk:
            step = STEP[index]; diff = s - pred; code = 0
            if diff < 0: code = 8; diff = -diff
            delta = step >> 3
            if diff >= step: code |= 4; diff -= step; delta += step
            if diff >= step >> 1: code |= 2; diff -= step >> 1; delta += step >> 1
            if diff >= step >> 2: code |= 1; delta += step >> 2
            pred = pred - delta if code & 8 else pred + delta
            pred = max(-32768, min(32767, pred))
            index = max(0, min(88, index + IDX[code]))
            nib.append(code)
        for i in range(0, BLOCK, 2):
            outb.append(nib[i] | (nib[i + 1] << 4))
    return bytes(outb)

ns = nm = 0; pcm_bytes = 0
for p in ([] if args.skip_audio else sorted(glob.glob(os.path.join(src, "audio", "sfx", "*.wav")))):
    w = wave.open(p, "rb")
    ch, rate, n = w.getnchannels(), w.getframerate(), w.getnframes()
    a = array.array("h"); a.frombytes(w.readframes(n))
    if ch == 2: a = array.array("h", [(a[i] + a[i + 1]) // 2 for i in range(0, len(a), 2)])
    data = adpcm(a.tolist())
    entries.append(("sfx/" + os.path.basename(p)[:-4], struct.pack("<III", rate, len(a), BLOCK) + data))
    ns += 1; pcm_bytes += len(a) * 2
for p in ([] if args.skip_audio else sorted(glob.glob(os.path.join(src, "audio", "music", "*.ogg")))):
    shutil.copy2(p, os.path.join(out, "music", os.path.basename(p))); nm += 1
log(f"sfx: {ns} clips, {pcm_bytes / 1e6:.1f} MB pcm -> adpcm; music: {nm} ogg streamed")

# ---------------------------------------------------------------- archive
# "KNLP" v1, count, then entries (name, offset u64, length u32), data follows
names = [n.encode("utf-8") for n, _ in entries]
hdr = 12 + sum(2 + len(n) + 12 for n in names)
off = hdr
dirb = bytearray()
for (n, data), nb in zip(entries, names):
    dirb += struct.pack("<H", len(nb)) + nb + struct.pack("<QI", off, len(data))
    off += len(data)
dst = os.path.join(out, "kingdom.knlpack")
with open(dst, "wb") as f:
    f.write(b"KNLP" + struct.pack("<II", 1, len(entries)))
    f.write(dirb)
    for _, data in entries: f.write(data)
validate_pack(dst)
log(f"wrote {dst}: {len(entries)} entries, {os.path.getsize(dst) / 1e6:.1f} MB (+ music/)")
