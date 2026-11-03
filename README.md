# Kingdom: New Lands on Miyoo Mini / Mini Plus

**Experimental candidate: 1.1.0-rc6. Full gameplay validation is pending.**

Please wait before buying the PC game specifically for this port. Tutorial, menu/save navigation, upgrades, coin-pouch physics and long-session stability still need a complete playthrough. The previous v1.0 release had installation and runtime defects. A successful boot does not establish that the full game works.

This port runs the player's original game logic under a replacement engine. You must supply files from your own PC copy. Release archives contain no original game assemblies or assets.

## Player installation

1. Extract the **complete candidate ZIP** onto the SD card root. It contains the `Roms` folder and the public runtime. Keep the files from one release together.
2. Copy your complete original **Kingdom_Data** folder into `Roms/PORTS/Games/KingdomNewLands/`, beside `knl.sh`. The result must contain `KingdomNewLands/Kingdom_Data/Managed/Assembly-CSharp.dll`.
3. Safely eject the card, insert it into the Miyoo, refresh Ports and launch **Kingdom New Lands**. First launch displays **PREPARING GAME FILES — THIS IS A ONE-TIME PROCESS**, with stage and percentage progress. Keep the device charged and leave time for sound conversion. **B** pauses preparation; launching again resumes verified checkpoints.

Players need no Python, compiler, FFmpeg, network connection or SSH for this route. Conversion happens on the Miyoo; it is not source compilation. Supported files are verified before use. An error screen explains missing, damaged or unsupported files. Conversion stages separately, retains an existing installation for rollback, and never replaces `save/`. See [first-launch details and device measurements](docs/ON_DEVICE_IMPORT.md).

For an update to an existing prepared installation, back up `save/`, then overlay the complete release's `Roms` folder. The launcher accepts the verified existing pack and game assemblies without requiring a new raw-data conversion. Native preparation keeps `.install-rollback/` copies; retain them until save/reload and gameplay pass. A previous PC-setup backup is named `KingdomNewLands.backup-<timestamp>`.

The optional **PC preparation route** remains available: extract the ZIP into a writable computer folder, run `setup_windows.bat` or `./setup.sh`, select your `Kingdom_Data` and SD root, and follow the prompts. It requires Python (Windows: 64-bit 3.11; Linux/macOS: 3.10–3.12), Tkinter for the picker, internet for initial dependencies, and FFmpeg for sound. Without FFmpeg, setup explicitly offers a silent test. This fallback has been exercised on Linux; Windows/macOS still need validation. It is useful when you prefer conversion on the PC.

### Supported data

The verified input is Kingdom: New Lands **1.2.8**, Unity **2017.1.2p1**, identified by the fingerprints in `tools/supported_build.json`. Store names alone do not establish compatibility. Other builds stop with an explanation; developers may use `--allow-unsupported` for desktop investigation, which marks data unverified. The stock Miyoo launcher and native importer accept only the verified build. Do not recommend unverified combinations to players.

Typical Windows paths:

- Steam: `C:\Program Files (x86)\Steam\steamapps\common\Kingdom New Lands\Kingdom_Data`
- GOG: `C:\GOG Games\Kingdom New Lands\Kingdom_Data`
- Epic: `C:\Program Files\Epic Games\KingdomNewLands\Kingdom_Data`

Other Steam libraries and storefront paths can be selected manually.

### Command-line alternative

Inside an isolated Python environment, install `tools/requirements.txt`, then:

```bash
python prepare_data.py "/path/to/Kingdom_Data" --out "/path/to/SD-root"
```

Use `--skip-audio` only for a silent test. Each conversion uses a fresh temporary directory. Invalid data and incomplete conversions fail before replacing the installation. The preparer writes `prepare.log`; the completed game folder contains `installation.json` with source and installed-file fingerprints. Do not run setup against an installation while the game is running.

## Controls and saves

- D-pad: move; Down/A/X: pay; B/L/R: gallop.
- Start: game menu; A: confirm; B/Select: cancel.
- **Menu + Start: emergency exit to Onion. It does not request a game save.** Use the game's save option first. Menu/save navigation is part of the pending gameplay validation.
- Save files live in `Roms/PORTS/Games/KingdomNewLands/save/`. Back them up before testing or updating.

## Reporting a failure

For first-launch failures, attach `logs/prepare.log` and `logs/prepare-check.log` from the game folder. For PC-setup failures, attach `prepare.log` from beside the preparer. For game failures, attach `Roms/PORTS/Games/KingdomNewLands/logs/knl.log` and describe the steps leading to the failure. Include candidate version, Onion version, game source/build, whether the save is new or existing, and whether sound was enabled. Logs may contain local paths; review them before posting.

The Miyoo candidate uses a top-right coin counter and lighter decorative effects by default. Overflow is a measured approximation of the original pouch, with no drops at 40 coins or below. [Counter behavior and measurements](docs/POUCH-COUNTER.md) and [visual changes](docs/LITE-VISUALS.md) describe the tradeoffs and optional modes.

The current local candidate repairs pooled enemy animation reuse and persistent day labels, adds bounded rain/snow and water detail, and reduces physics/particle/render preparation work. The catapult AI remains original; joint/contact parity and long-session enemy visibility still need gameplay validation. See [rc6 combat and installation validation](docs/VALIDATION-1.1.0-rc6.md) and [rc5 repairs and remaining issues](docs/VALIDATION-1.1.0-rc5.md) and the [performance budget](docs/PERFORMANCE-BUDGET.xlsx).

Please report tutorial completion, menu/save navigation and upgrades even if there is no crash. No exception in a log does not prove correct behavior. See [the testing checklist](docs/TESTING.md).

## Developer build and checks

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

Kingdom: New Lands belongs to Noio and Raw Fury. This independent port is not endorsed by them. Third-party runtime notices accompany candidate archives.
