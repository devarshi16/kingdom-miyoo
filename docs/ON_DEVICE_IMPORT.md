# First-launch import investigation

Status: **stage 1 prototype implemented; full first-launch import is not implemented.** The
native reader validates the nine supported serialized files, checks their SHA-256 fingerprints,
and emits the complete format-17 type table and object index without reading object payloads.
It does not yet convert MonoBehaviour payloads, textures, fonts, or audio, or produce a runtime
pack. It is not wired to the launcher and must not be presented as a playable installation path.

## Existing evidence and constraints

The current Python converter took 34.8 seconds on the development PC and peaked at 522756 KiB
RSS (510.5 MiB). The inspected Onion 4.3.1-1 device reported MemTotal 103360 KiB, MemAvailable
about 74180 KiB and 128 MiB unused swap. Those measurements show that the Python pipeline cannot
move unchanged onto the device; they do not establish a lower bound for a native converter.

The existing conversion reads nine serialized files and emits 16,880 objects, including 4,357
MonoBehaviours. It also exports 2,205 sprites, 96 textures, 297 audio clips, and fonts/text. The
largest RGBA atlas is 2048×2048 (16 MiB), and the resources audio stream is about 218 MiB on disk.
Texture and audio streams need not all reside in RAM at once. MonoBehaviour type trees are absent
from this build, so a complete reader must reconstruct their layouts from the user's own managed
assemblies and retain the existing per-build quirks (including header alignment and string-list
handling).

## Stage 1 prototype

`tools/native_import/native_index.c` is a standalone C prototype. It supports Unity serialized
format 17 / Unity 2017.1.2p1. It validates file size, metadata bounds, target platform fields,
type records (including MonoScript IDs and type hashes), optional flattened type trees, object
IDs, type indices, sizes, and payload ranges. It streams SHA-256 over the nine serialized files
against the values in `tools/supported_build.json`; this stage does not yet verify the managed
DLLs or resource-stream files. It only holds one file's type/object index at a time. Object
payloads are not decoded.

The progress output on stderr begins with `Preparing game files — this is a one-time process`,
then reports source verification and each indexed file. Failures use an `ERROR:` line and a
nonzero exit code. The JSON lines on stdout are private derived metadata and should stay outside
the repository. The test-only `fixture-index` command bypasses source fingerprints and is not a
player command.

Build and run on a development host:

```sh
gcc -O2 -Wall -Wextra -Werror tools/native_import/native_index.c -o /tmp/native_index
/usr/bin/time -v /tmp/native_index index \
  /home/devarshi/Games/KingdomNewLands/Kingdom_Data \
  >/tmp/knl-native-index.jsonl 2>/tmp/knl-native-status.log
python3 tools/native_import/test_native_index.py
```

The current x86-64 runs indexed all 16,880 records in 0.02–0.03 seconds with 1,792 KiB peak RSS;
that number includes only fingerprinting and metadata indexing, and says nothing about device
import time or decode memory. The fixture suite covers valid records, restart repeatability,
truncation, declared-size mismatch, out-of-range payloads, and missing input. Against the existing
local UnityPy extraction, all nine per-file PathID sets matched exactly.

Cross-compile for the project's ARM hard-float target (the output is a private build artifact):

```sh
/home/devarshi/Recompilation/tools/prebuilt/bin/arm-linux-gnueabihf-gcc \
  -O2 -Wall -Wextra -Werror tools/native_import/native_index.c \
  -o /home/devarshi/Recompilation/work/knl-rc5/native-import/native_index.armhf
```

When the owner runs the device check, record the exact command, elapsed time, peak RSS, free SD
space, available memory, and Onion version. For this stage, the command is
`native_index.armhf index <Kingdom_Data> >/tmp/knl-native-index.jsonl`; keep the output in
private storage. The prototype was subsequently run on the Miyoo: all nine files / 16,880 objects indexed in **0.506 seconds**, with **1,288 KiB peak RSS**. The device was idle in Onion, governor ondemand, maximum 1200 MHz, MemAvailable 80,944 KiB. These are measured index-stage costs, not a full conversion time or memory estimate. The private original files and index stayed in the isolated QA installation; the normal game and saves were untouched. See `docs/evidence/native-index-device.json`.

## Recovery and installation contract for a future complete importer

The index prototype writes to stdout and does not alter an installation. Before any full import
is connected to the launcher, its output should follow this same-filesystem staging contract:

1. Verify every required input fingerprint and available SD space before creating output. Keep
   the original `Kingdom_Data` read-only. Hash the managed DLLs and resource streams as well as
   the nine serialized files before a full conversion can claim the verified build.
2. Write only under a sibling staging directory, such as
   `Roms/PORTS/Games/.KingdomNewLands.data.new/`. Store the profile and source hashes in a
   journal. Write each file to a temporary name, flush it, then rename it into the stage. A
   restart may reuse only files whose journal entry and output hash both match the same source
   fingerprint; otherwise redo that file. Never mix partial output from different source builds.
3. Keep the active game data and `save/` untouched throughout conversion. Put save data outside
   the replaceable imported-data directory. Do not remove an old installation to make space.
4. After all conversions pass validation, write and flush a complete manifest, then activate via
   same-filesystem renames. First move existing imported data to a uniquely named backup; next
   move the completed stage into place. A small activation journal must make interruption between
   these renames recoverable: if the new target is absent, restore the backup; if the new target
   validates, finish activation and retain the backup until the game/save checks pass. Startup
   recovery must run before launching the game.
5. Report each stage and actionable failure in the UI. Do not show a success state until the
   activated manifest and installed-file hashes validate. Keep the original save path stable.

These are design requirements for the full importer, not behavior implemented by stage 1.

## Remaining work before first-launch import

- Reconstruct and stream MonoBehaviour values using the user's managed assemblies; verify all
  4,357 script references and compare decoded types/fields against UnityPy output.
- Convert one texture or bounded tile at a time while preserving Sprite rect, pivot, mesh,
  packing, alpha, and atlas semantics. Measure native buffers and peak RSS.
- Establish the ARM-compatible audio decoder, Unity/FMOD format coverage, licensing notices,
  and streaming/cache policy. The current conversion depends on UnityPy and FFmpeg.
- Convert fonts, text, non-sprite textures, and other referenced payloads; build pack sections
  incrementally and validate them with the existing runtime pack checks.
- Add managed-DLL/resource-stream fingerprints, preflight free-space estimates, persistent
  journal recovery, and explicit disk-full/corrupt-source failures.
- Integrate the UI and launcher, then validate full first launch, interruption/restart, existing
  installation preservation, save/reload, and representative gameplay on real hardware.

Do not publish a first-launch time, memory-feasibility claim, or successful import claim until the
full texture/audio/font pipeline and the device restart/save checks have been measured.
