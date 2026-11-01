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

### Step 2: Prepare Your Game Data (Windows, Mac, or Linux)
Run the automated asset preparer on your PC to convert and stage your game data:

```bash
# 1. Install standard dependencies (pure Python, no C compiler required)
pip install UnityPy Pillow dnfile TypeTreeGeneratorAPI

# 2. Run data preparer (point it to your game's Kingdom_Data and your SD card)
python prepare_data.py "C:\path\to\Kingdom New Lands\Kingdom_Data" --out "E:\"
```

*(On macOS/Linux, replace the paths accordingly, e.g. `--out /media/$USER/SDCARD`).*

### Step 3: Play!
Insert the SD card into your Miyoo Mini, go to **Games › Ports › Refresh ROMs**, and launch **Kingdom New Lands**!

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
