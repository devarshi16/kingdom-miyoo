# Kingdom: New Lands — Miyoo Mini / Mini Plus Port

<p align="center">
  <img src="media/gameplay.gif" alt="Kingdom: New Lands on Miyoo Mini Plus" width="360" />
</p>

A native, clean-room port of **Kingdom: New Lands** for the **Miyoo Mini** and **Miyoo Mini Plus** running OnionOS.

This is a **Bring Your Own Data (BYOD)** port. You must provide game files from your own legally purchased copy of *Kingdom: New Lands* (Steam, Epic Games Store, or GOG). **No game assets or proprietary code are distributed here.**

---

## 🚀 Easy Installation (No Compilation Needed)

**You do NOT need to compile anything or use Linux/WSL.** All required ARM binaries and the clean-room engine runtime are pre-compiled in the release zip.

### Step 1: Download Release
1. Download [`KingdomNewLands-MiyooMini-v1.0.zip`](https://github.com/devarshi16/kingdom-miyoo/releases) from Releases.
2. Extract the `Roms/` folder directly to the root of your Miyoo SD card.

### Step 2: Prepare Your Game Data

#### 🪟 On Windows (Easiest — Automated):
1. Download or clone this repository to your PC.
2. Double-click **`setup_windows.bat`**.
   * It will automatically verify Python, install dependencies, auto-detect your Steam/GOG/Epic install, and stage files directly to your SD card!

#### 🐧 On macOS / Linux (or Manual Windows):
```bash
# 1. Install dependencies (ffmpeg is recommended for soundtrack & sound effects)
pip install UnityPy Pillow dnfile TypeTreeGeneratorAPI

# 2. Run data preparer (point it to your game's Kingdom_Data and your SD card)
python prepare_data.py "/path/to/Kingdom New Lands/Kingdom_Data" --out "/media/$USER/SDCARD"
```
*(On Windows manual CLI, replace with e.g. `python prepare_data.py "C:\GOG Games\Kingdom New Lands\Kingdom_Data" --out "E:\"`).*
*(Add `--skip-audio` if you do not have `ffmpeg` installed and wish to test without audio).*

> [!TIP]
> **WSL Users**: 
> 1. Always clone this repository inside your WSL home directory (`~`, e.g. `~/kingdom-miyoo`), **not** under `/mnt/c/`, as Windows-WSL filesystem bridging (`drvfs`) is drastically slower and can break Python packages.
> 2. Your SD card (e.g. drive `E:`) is accessible in WSL under `/mnt/e` (e.g. `--out /mnt/e`).

### Step 3: Play!
Insert the SD card into your Miyoo Mini, go to **Games › Ports › Refresh ROMs**, and launch **Kingdom New Lands**!

---

## 🪵 Troubleshooting & Sharing Logs

If the game crashes back to the menu or shows a black screen, the port writes an execution log every time it starts:

### Finding Your Log File:
On your SD card, open:
📁 `Roms/PORTS/Games/KingdomNewLands/logs/knl.log`

### Reporting an Issue:
If you encounter any crash or bug, please **open an issue on GitHub and attach or paste your `knl.log` file**. It contains the exact Mono stack trace and error message, which makes diagnosing and fixing your issue fast!

> [!NOTE]
> **Windows Defender Notice**: The release zip contains **zero `.exe` files** (the launcher uses `knl.dll` executed directly by Mono on the device). If generic heuristics flag the archive because of unfamiliar Linux ARM binaries or shell scripts, it is a false positive. All source code is completely open in this repository.

---

## 📦 Game Data Locations (BYOD)

Locate the `Kingdom_Data` directory from your PC installation:

| Platform | Default Path |
| :--- | :--- |
| **Steam (Windows)** | `C:\Program Files (x86)\Steam\steamapps\common\Kingdom New Lands\Kingdom_Data` |
| **Steam (Linux)** | `~/.local/share/Steam/steamapps/common/Kingdom New Lands/Kingdom_Data` |
| **Steam (macOS)** | `~/Library/Application Support/Steam/steamapps/common/Kingdom New Lands/Kingdom_Data` |
| **Epic Games (Windows)** | `C:\Program Files\Epic Games\KingdomNewLands\Kingdom_Data` |
| **Epic Games (Linux - Native / Heroic)** | `~/Games/KingdomNewLands/Kingdom_Data` |
| **Epic Games (Linux - Lutris)** | `~/Games/epic-games-store/drive_c/Program Files/Epic Games/KingdomNewLands/Kingdom_Data` |
| **Epic Games (macOS)** | `/Users/Shared/Epic Games/KingdomNewLands/Kingdom_Data` |
| **GOG (Windows)** | `C:\GOG Games\Kingdom New Lands\Kingdom_Data` |
| **GOG (Linux)** | `~/GOG Games/Kingdom New Lands/Kingdom_Data` |

---

## 🤖 Install using an AI Agent

If you use an AI coding agent (Antigravity, Cursor, Claude Code, Aider), you can give it this prompt to set up everything automatically:

```text
Install the Kingdom: New Lands port from github.com/devarshi16/kingdom-miyoo on my Miyoo Mini Plus at IP [your_miyoo_ip].
Locate the Kingdom_Data folder from my PC game installation (Steam/Epic/GOG).
Run python prepare_data.py to extract and pack the game data, sync the Roms folder to /mnt/SDCARD/ over SSH, and verify the game boots cleanly.
```

---

## 🛠️ Building Engine from Source (Developers Only)

> [!IMPORTANT]
> **Players do NOT need to build from source.** `compile.sh` compiles the desktop engine test harness unless you have the Miyoo cross-compiler toolchain installed. To play on your Miyoo Mini, follow the **Easy Installation (Steps 1 & 2)** above using the pre-compiled release package.

If you are modifying the clean-room C blitter or C# UnityEngine replacement:

### Prerequisites (Linux / WSL)
```bash
sudo apt update && sudo apt install -y build-essential mono-devel libsdl2-dev python3 python3-pip ffmpeg
```

### Build Pipeline
```bash
git clone https://github.com/devarshi16/kingdom-miyoo.git
cd kingdom-miyoo
./compile.sh "/path/to/Kingdom_Data"
```

---

## 📜 Legal & Disclaimer

*Kingdom: New Lands* is © **Noio** and **Raw Fury**. This project is an independent re-implementation created for educational and preservation purposes. It is not affiliated with or endorsed by Noio or Raw Fury.
