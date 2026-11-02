# Kingdom: New Lands on Miyoo Mini / Mini Plus

**Experimental candidate: 1.1.0-rc4. Full gameplay validation is pending.**

Please wait before buying the PC game specifically for this port. Tutorial, menu/save navigation, upgrades, coin-pouch physics and long-session stability still need a complete playthrough. The previous v1.0 release had installation and runtime defects. A successful boot does not establish that the full game works.

This port runs the player's original game logic under a replacement engine. You must supply files from your own PC copy. Release archives contain no original game assemblies or assets.

## Player installation

1. Download the **complete candidate ZIP** from the project's release assets when a candidate is published. Extract it into a writable folder on your computer. Do not mix files from different releases.
2. On Windows, install **64-bit Python 3.11** from [python.org](https://www.python.org/downloads/windows/) if needed. Enable the launcher and PATH options. Double-click `setup_windows.bat`.
3. On Linux/macOS, run `./setup.sh` using Python 3.10–3.12. The folder picker requires Tkinter; on Ubuntu install `python3-venv` and `python3-tk` if missing. This route has been exercised on Linux; Windows/macOS still require validation on those systems.
4. Select your **Kingdom_Data** folder, then the **root of the SD card** (or a local output folder). Setup installs Python dependencies in its own `.player-venv`; an internet connection is needed for the first setup.
5. For sound, install **FFmpeg** and make it available on PATH before setup. The Windows launcher also checks `tools/ffmpeg/ffmpeg.exe` if you provide a trusted portable copy there. When FFmpeg is absent, setup offers an explicitly **silent** test installation. It does not silently claim that sound was installed.
6. Wait for validation to finish. Existing installations and saves are retained in `Roms/PORTS/Games/KingdomNewLands.backup-<timestamp>`. Keep this backup until save/reload and gameplay checks pass.
7. Safely eject the card, insert it in the Miyoo, refresh Ports and launch **Kingdom New Lands**. If you selected a local output folder, first copy its `Roms` folder onto the SD card root.

The ZIP includes the runtime and preparer. Players do not need Git, WSL, a compiler, or a separate source download. This folder-picker setup still requires Python and installs dependencies; it is **not yet a standalone Windows installer**. A raw-data, first-launch importer on the Miyoo has not been implemented or benchmarked.

### Supported data

The verified input is Kingdom: New Lands **1.2.8**, Unity **2017.1.2p1**, identified by the fingerprints in `tools/supported_build.json`. Store names alone do not establish compatibility. Other builds stop with an explanation; developers may explicitly try `--allow-unsupported`, which marks the installation unverified. Do not recommend unverified data combinations to players.

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

For setup failures, attach `prepare.log` from beside the preparer. For game failures, attach `Roms/PORTS/Games/KingdomNewLands/logs/knl.log` and describe the steps leading to the failure. Include candidate version, Onion version, game source/build, whether the save is new or existing, and whether sound was enabled. Logs may contain local paths; review them before posting.

The Miyoo candidate uses a top-right coin counter and lighter decorative effects by default. Overflow is a measured approximation of the original pouch, with no drops at 40 coins or below. [Counter behavior and measurements](docs/POUCH-COUNTER.md) and [visual changes](docs/LITE-VISUALS.md) describe the tradeoffs and optional modes.

The current local candidate also repairs catapult wheel hinges, torch reflection layers, baked menu font layout and the day HUD. Second-island performance and clustered enemy visibility still need gameplay validation; see [rc4 measurements and remaining issues](docs/VALIDATION-1.1.0-rc4.md).

Please report tutorial completion, menu/save navigation and upgrades even if there is no crash. No exception in a log does not prove correct behavior. See [the testing checklist](docs/TESTING.md).

## Developer build and checks

Linux prerequisites: Mono development tools (`mcs`, `mono`), GCC/G++, SDL2 development files, Python dependencies, and FFmpeg. The Miyoo build additionally needs the compatible ARM toolchain, SigmaStar MI headers/libraries and Mono ARM runtime described in `engine/build_miyoo.sh`; a desktop build is not a device build.

```bash
./compile.sh "/path/to/Kingdom_Data"
python3 -m unittest discover -s tests -v
./tests/run_runtime.sh
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
