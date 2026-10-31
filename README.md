# Kingdom: New Lands — Miyoo Mini / Mini Plus Port

<p align="center">
  <img src="media/gameplay.gif" alt="Kingdom: New Lands on Miyoo Mini Plus" width="360" />
</p>

A native, clean-room port of **Kingdom: New Lands** for the **Miyoo Mini** and **Miyoo Mini Plus** running OnionOS.

This is a **Bring Your Own Data (BYOD)** port. You must provide game files from your own legally purchased copy of *Kingdom: New Lands* (Steam, Epic Games Store, or GOG). **No game assets or proprietary code are distributed here.**

---

## 📦 Bring Your Own Data

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

## 🚀 Installation (Pre-built Release)

1. Download the latest `KingdomNewLands-Miyoo.zip` from **Releases**.
2. Extract the `Roms/` folder to the root of your Miyoo SD card.
3. On your PC, convert and pack your game data:
   ```bash
   ./compile.sh "/path/to/Kingdom_Data"
   ```
4. Copy the generated files from `engine/build-miyoo/Roms/` to your SD card.
5. On your Miyoo, go to **Games › Ports › Refresh ROMs**, then launch **Kingdom New Lands**.

---

## 🛠️ Compilation

### Ubuntu / Debian
```bash
# 1. Install dependencies
sudo apt update && sudo apt install -y build-essential mono-devel libsdl2-dev python3 python3-pip ffmpeg

# 2. Clone and build
git clone https://github.com/<your-username>/KingdomNewLandsMiyooMini.git
cd KingdomNewLandsMiyooMini
./compile.sh "/path/to/Kingdom_Data"

# 3. Copy output to SD card
cp -r engine/build-miyoo/Roms/* /media/$USER/<SDCARD>/Roms/
```

### macOS
```bash
# 1. Install dependencies via Homebrew
brew install mono-libgdiplus sdl2 ffmpeg python

# 2. Build desktop engine and pack data
git clone https://github.com/<your-username>/KingdomNewLandsMiyooMini.git
cd KingdomNewLandsMiyooMini
./compile.sh "/path/to/Kingdom_Data"
```
*(To cross-compile the ARM device binary on macOS, use Docker or the pre-compiled release binary).*

### Windows (WSL)
```bash
# 1. Open WSL terminal (Ubuntu)
wsl

# 2. Install dependencies
sudo apt update && sudo apt install -y build-essential mono-devel libsdl2-dev python3 python3-pip ffmpeg

# 3. Build using your Windows game install path
git clone https://github.com/<your-username>/KingdomNewLandsMiyooMini.git
cd KingdomNewLandsMiyooMini
./compile.sh "/mnt/c/Program Files (x86)/Steam/steamapps/common/Kingdom New Lands/Kingdom_Data"

# 4. Copy engine/build-miyoo/Roms to your SD card
```

---

## 🤖 Install using AI Agent

You can paste this prompt directly into an AI coding agent (Antigravity, Cursor, Claude Code, Aider, etc.) to set up and deploy the port automatically:

```text
Install the Kingdom: New Lands port from github.com/<your-username>/KingdomNewLandsMiyooMini on my Miyoo Mini Plus available at IP [your_miyoo_ip].
For the game data, it is installed on my system at [path_to_game or steam/epic/gog location].
Run ./compile.sh to build and pack the assets, sync the Roms directory to /mnt/SDCARD/ over SSH, and verify that the game boots cleanly.
```

---

## 📜 Legal & Disclaimer

*Kingdom: New Lands* is © **Noio** and **Raw Fury**. This project is an independent re-implementation created for educational and preservation purposes. It is not affiliated with or endorsed by Noio or Raw Fury.
