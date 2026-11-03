# Development and validation

Start with [project decisions](DECISIONS.md). Player instructions live in the
[root README](../README.md); the [first-launch implementation](ON_DEVICE_IMPORT.md),
[rc6 validation](VALIDATION-1.1.0-rc6.md) and
[performance budget](PERFORMANCE-BUDGET.xlsx) contain the engineering evidence.

## Build and checks

Linux prerequisites: Mono development tools (`mcs`, `mono`), GCC/G++, SDL2 development files, Python dependencies, and FFmpeg. The Miyoo build additionally needs the compatible ARM toolchain, SigmaStar MI headers/libraries and Mono ARM runtime described in `engine/build_miyoo.sh`. Building the importer needs CMake, Mono.Cecil 0.11, the Python dependencies and pinned codec sources. `engine/build_miyoo.sh --runtime-only OUT` creates the public payload without a prepared game pack. Set `KNL_IMPORT_CACHE` to a data volume for dependency builds, and optionally `KNL_IMPORT_SOURCES` to pinned checkouts. A desktop build is not a device build.

```bash
./compile.sh "/path/to/Kingdom_Data"
python3 -m unittest discover -s tests -v
./tests/run_runtime.sh
./tests/run_animator_pooling.sh
./tests/run_sprite_culling.sh
./tests/run_sprite_lighting.sh
./tests/run_day_hud.sh
./tests/run_lite_weather.sh
./tests/run_keyboard.sh
./tests/run_component_lookup.sh
KNL_CAST_REFERENCE_RUNTIME=/path/to/rc5-runtime ./tests/run_collider_casts.sh
KNL_IMPORT_NATIVE_DIR=/path/to/host-import-helper ./tests/run_native_import.sh
./tests/run_physics_performance.sh
./tests/run_transform_cache.sh
./tests/run_hinges.sh
./tests/run_enemy_indicators.sh
./tests/run_rendering_bounds.sh
./tests/run_ui.sh
./tests/run_native.sh
./tests/run_visual.sh
./tests/run_coin_counter.sh
./tests/run_occlusion.sh
./tools/package_release.sh
```

`compile.sh` creates a fresh `.build-data.*` folder and prints whether an ARM package was built. `engine/run.sh --data pack --boot --window` runs an existing pack for desktop investigation. `tools/package_release.sh` explicitly selects redistributable runtime files and bundles the player setup tools; it does not copy the developer's game data. It emits a versioned ZIP, SHA-256 checksum and an internal manifest recording the source commit and dirty-tree state. Commit the candidate source before publishing.

The managed host is named `knl.dll` consistently, but is still a managed executable. Its extension is not a malware assessment. Report antivirus detections with the exact archive hash and detection name; do not disable protection to install a flagged download.

## PC preparation support

Use the complete extracted release ZIP, including its runtime and unmodified
manifest files. A source checkout alone is not the player installer.

- Recommended Python: 3.11. The tested Linux path supports 3.10–3.12; Windows
  instructions target 64-bit 3.11. Windows/macOS preparation still needs validation.
- CLI preparation needs Python dependencies from `tools/requirements.txt` and
  FFmpeg on PATH for sound. It does not need Tkinter.
- `setup_windows.bat` and `setup.sh` create a local environment and open the
  Tkinter folder picker. Linux may need its distribution's Tkinter package.
- Initial dependency installation requires internet access. Allow at least 2 GiB
  of temporary space plus SD space for staging and the previous installation.
- `--skip-audio` explicitly creates a silent test. The stock launcher accepts only
  the fingerprinted supported build. `--allow-unsupported` is for desktop
  investigation and marks data unverified.
- Do not prepare over an installation while the game is running. Existing saves
  are copied into the staged installation; old installations are retained as
  `KingdomNewLands.backup-<timestamp>`.

## Release media

The player-facing photo and GIFs under `docs/media/` were supplied by the project
owner for this README and release. See [media provenance](media/README.md) for
cuts and metadata handling. This permission does not cover unrelated QA captures.
Keep extracted game assets, original DLLs, packs, saves and private benchmark
captures out of source control and release runtime files.

When adding a README-linked document or media file, add it to `PLAYER` in
`tools/package_release.py` so offline ZIP readers receive it too. Package only
from a clean source commit and verify the public allowlist and manifest hashes.
