# First-launch import investigation

Status: **not implemented**. A native importer remains a separate project, not a prerequisite for playing a correctly prepared package.

## Evidence from the verified 1.2.8 input

The full current Python converter took 34.8 seconds on the development PC and peaked at 522756 KiB RSS (510.5 MiB). The inspected Onion 4.3.1-1 device reported MemTotal 103360 KiB, MemAvailable about 74180 KiB and 128 MiB unused swap. This pipeline cannot be moved unchanged onto that device; these measurements do not establish a lower bound for a different importer.

Extraction decoded nine serialized files and 2205 sprites, 96 textures and 297 audio clips. The largest RGBA atlas is 2048×2048 (16 MiB), while the resources audio stream is about 218 MiB on disk. Texture and audio streams need not all reside in RAM at once. Current KNL packs already demonstrate title-specific compact/lazy object access, indexed textures and streamed music at runtime.

## Bounded prototype sequence

1. Detect exact source fingerprints and Unity serialization version before allocating asset data. Index objects and resource offsets incrementally. The `tools/import_probe.c` helper reads only headers and bounded metadata strings; it is reconnaissance, **not a converter**.
2. Establish a schema for this supported build, including MonoBehaviour field layouts reconstructed from the player's original assemblies. The original data lacks embedded layouts for those scripts. A simple file copier cannot generate the KNL object pack.
3. Convert one texture at a time or in tiles, keeping original sprite rect/pivot/packing/alpha semantics. Measure peak RSS plus native buffers, not just the texture's nominal dimensions.
4. Prototype audio separately. The existing converter relies on UnityPy/FMOD sample decoding and FFmpeg; an ARM-compatible decoder, format handling and required notices must be established. Do not claim this dependency is solved by a metadata scanner.
5. Write temporary pack sections and offset tables incrementally; check free space, checksum output, show progress and support safe restart after power loss. Activate the completed installation only after validation. Do not compile the runtime on the player's device.
6. Benchmark on real hardware with game runtime/MainUI memory conditions recorded. A prototype should leave sufficient OS headroom without depending on SD swap. Do not publish a one-time import-time estimate until the actual texture/audio pipeline is measured.

Completion requires a player copying original files to the card, a successful recoverable first-launch import, and the same gameplay/save tests as the desktop-prepared package. Header scanning alone cannot establish completion or memory feasibility for all decoding stages.
