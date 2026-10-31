# Kingdom New Lands → Miyoo Mini Plus

A bring-your-own-data port of *Kingdom New Lands* (Noio / Raw Fury) to the Miyoo Mini Plus
(OnionOS). **This repository contains no game code or assets.** You supply `Kingdom_Data` from
your own legally owned copy; the tools here convert it on your PC at install time.

Reference build: Kingdom New Lands **1.2.8** · Unity **2017.1.2p1** (Mono backend).
Other builds may differ; the tools report what they see.

## How it works

```
your Kingdom_Data ──(PC, install time)──► tools/convert.py ──► extracted/ ──► pack (M3) ──► SD card
                                                                                │
device:  Mono (AOT) ── Assembly-CSharp.dll (yours, unmodified)                  │
                     └─ UnityEngine.dll  (ours: clean-room replacement) ◄───────┘
                          └─ native core: 320×240 software renderer → 2× blit,
                             Box2D, audio mixer (Vorbis stream + PCM SFX), buttons
```

Why this shape, and the rest of the design choices: [`docs/DECISIONS.md`](docs/DECISIONS.md).
What the replacement engine must implement: [`docs/unity_api_surface.md`](docs/unity_api_surface.md)
(generated from your DLLs).

## Quick start (Milestone 0 tools)

Needs Python ≥ 3.9 and ffmpeg (with libvorbis) on the PC.

```bash
./tools/setup.sh                                                 # local deps (tools/.venv or tools/.pydeps)
./tools/run.sh api_surface.py ~/Games/KingdomNewLands/Kingdom_Data   # -> docs/unity_api_surface.{md,json}
./tools/run.sh convert.py     ~/Games/KingdomNewLands/Kingdom_Data   # -> extracted/ + extracted/report.json
```

`convert.py --skip audio` gives a fast run (~minutes) when you only need scenes/sprites.

## Milestones

| | Goal | Done when |
|---|---|---|
| **M0** ✅ | Tooling: API surface + BYOD extractor | done: 16,880 objects / 0 failures, 96 textures, 2,205 sprites, 297 audio clips; checklist = 222 Unity types / 1,110 members + Rewired 64 / 248 |
| **M1** ✅ | Desktop: static scene renders | done: game assemblies load against our `UnityEngine.dll`; 872 scene objects, 65,722 fields deserialized (0 errors); all 37 level blocks render at 320×240 (~5 ms/frame desktop); smoke test runs 160/195 game lifecycle calls |
| **M2** ✅ | Desktop: playable loop | done: boots from the real loader scene, 0 exceptions over 16,500 frames; island generates; ride, coin pickup, paying, building (campfire → town centre), tutorial, day → night → day; Greed spawn at the cliff portal on night 1, march in, knock coins off the king and run; edge indicators fire (D3). Known gaps → M3: night recolour shader, UI/text, audio, particles |
| **M3** ✅ | Menus, saves, audio; device pack format | done: pause/main menu with D-pad navigation, tutorial hints (controller glyphs), text from the game's pixel fonts; night darkness + torch/fire light pools (D16); sound: SFX, music, ambience through the game's mixers (D18); save + quit and continue restore the session; device pack: 38 MB managed heap instead of 256 MB, pixel-identical frames (D19) |
| M4 | Device bring-up | runs on Miyoo Mini Plus under OnionOS from the Ports menu |
| M5 | Performance + polish | stable 30 fps target, memory headroom, controls finalised |

## Desktop engine (Milestones 1–2)

Needs `mono-devel` (tested 6.8), `build-essential`, and `libsdl2-dev` for the window
(`sudo apt install mono-devel build-essential libsdl2-dev` on Ubuntu 22.04/24.04).

```bash
./tools/run.sh convert.py ~/Games/KingdomNewLands/Kingdom_Data   # once; sprites + audio
engine/build.sh ~/Games/KingdomNewLands/Kingdom_Data              # core + UnityEngine.dll + Rewired_Core.dll + knl.exe
engine/run.sh --boot --window                                     # play (M2)
```

Keys (mirroring the device buttons): **←/→ or A/D** ride · **S/↓ or X** pay (hold) ·
**Shift/C** gallop · **Enter/Space** submit · **Esc** start · **Tab** select · **F10** quit.
Time starts when you ride right past the first camp, as in the original.

Headless checks (no window): `engine/run.sh --boot --frames 3400 --input "520-2300:right,2350-2685:left,2750-3300:down" --trace-scenes --shot-every 100 --shots shots`
rides out for the starting coins, comes back and lights the campfire (add `--save <empty dir>` for a
fresh game: with an existing save the run continues it, so frames depend on the save); the log shows the king's x,
coins, day/time and enemy/edge-indicator counts. `engine/run.sh` lists all debug options.
M1 viewer modes still work (`--activate Blocks --camera -50,1.88`, `--shots-blocks`, `--smoke`).

## Device data pack (Milestone 3)

The Miyoo has 128 MB of RAM, so the device runs from a compact pack instead of `extracted/`:

```bash
./tools/run.sh pack.py                       # extracted/ -> pack/kingdom.knlpack + pack/music/ (~20 s)
engine/run.sh --data pack --boot --window    # same game, from the pack (what M4 copies to the SD card)
```

Sound needs `libsdl2-dev` at build time. `KNL_CONTROLS=gamepad engine/run.sh ...` shows the controller
glyphs the device will use. Saves go to `engine/build/save/` (`--save DIR` to change).

## Miyoo Mini Plus (Milestone 4)

Needs, outside this repo (paths are overridable, see the header of `engine/build_miyoo.sh`): ARM GNU
Toolchain 8.2-2018-08 (`arm-linux-gnueabihf-gcc` 8.2.1, matches the device's glibc 2.28), the
SigmaStar MI headers/libs (steward-fu/sdl2 `mini/`, commit 0631abc), and the Mono 6.8.0.105 armhf
packages for Debian 10 from download.mono-project.com (list in the script header). Why: D21.

```bash
engine/build.sh ~/Games/KingdomNewLands/Kingdom_Data && ./tools/run.sh pack.py   # desktop build + pack first
engine/build_miyoo.sh            # -> engine/build-miyoo/Roms/...  copy Roms/ to the SD card root
engine/build_miyoo.sh --push     # or copy over SSH (host alias "miyoo" in ~/.ssh/config)
```

On the device: Games › Ports › ~Refresh roms, then Strategy › Kingdom New Lands. Menu + Start quits.
Performance (M5, D22): `KNL_PROF=1` (or `2` for per-script detail) adds per-phase timings, allocations and GC
counts to the log; `KNL_SYNC=1` turns off the second-core rasteriser for comparison.
The log (boot time, `[perf]` lines every 300 frames) is `Roms/PORTS/Games/KingdomNewLands/logs/knl.log`.
Over SSH, `./knl.sh --frames 600 --shot-every 300 --shots logs` runs a timed session with PNG shots.

## Layout

```
docs/                 decisions, generated API checklist
tools/                PC-side scripts (api_surface.py, convert.py, pack.py, gen_shim.py + stage1dump.cs, setup.sh, run.sh)
engine/native/        knlcore.c — software renderer + SDL2/headless/Miyoo platform layer; knlaudio.c — mixer (C)
engine/miyoo/         device launcher (knl.sh, Onion .port shortcut), libgssapi stub for Mono (D21)
engine/UnityEngine/   our clean-room UnityEngine.dll (Core/ = Unity API, Runtime/ = KNL engine)
engine/Rewired/       our clean-room Rewired_Core.dll (input, D11)
engine/Host/          knl.exe desktop host
extracted/            conversion output from YOUR data (git-ignored — never commit or share)
pack/                 device pack built from extracted/ (git-ignored — never commit or share)
engine/build/, engine/build-miyoo/, engine/*/Generated/   build outputs derived from your DLLs (git-ignored)
```

## Legal

Kingdom New Lands is © Noio / Raw Fury. This project is not affiliated with them. Do not
commit or distribute anything from `extracted/` or from the game installation.
