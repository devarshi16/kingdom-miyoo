#!/usr/bin/env python3
"""
prepare_data.py — Zero-compile asset preparation tool for Kingdom: New Lands (Miyoo Mini).

Runs natively on Windows, macOS, and Linux without compilers, WSL, or Mono-devel.
Extracts game assets from your legally owned PC copy of Kingdom: New Lands and
stages them for your Miyoo SD card.

Usage:
    python prepare_data.py "<path_to_Kingdom_Data>" [--out "<path_to_sd_card_or_folder>"]

Example (Windows):
    python prepare_data.py "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Kingdom New Lands\\Kingdom_Data" --out "E:\\"

Example (macOS / Linux):
    python3 prepare_data.py ~/.steam/steam/steamapps/common/Kingdom\\ New\\ Lands/Kingdom_Data --out /media/user/SDCARD
"""

import os
import sys
import shutil
import argparse
import subprocess
from pathlib import Path

HERE = Path(__file__).resolve().parent

def check_dependencies():
    missing = []
    for pkg in ["UnityPy", "PIL", "dnfile"]:
        try:
            __import__(pkg)
        except ImportError:
            missing.append("Pillow" if pkg == "PIL" else pkg)
    
    if missing:
        print("[-] Missing required Python packages:")
        for m in missing:
            print(f"    • {m}")
        print("\nPlease install them by running:")
        print(f"    pip install {' '.join(missing)} TypeTreeGeneratorAPI\n")
        sys.exit(1)

def find_kingdom_data(path_str):
    p = Path(path_str).resolve()
    if p.is_dir() and p.name == "Kingdom_Data":
        return p
    if p.is_dir() and (p / "Kingdom_Data").is_dir():
        return p / "Kingdom_Data"
    return None

def main():
    parser = argparse.ArgumentParser(description="Prepare Kingdom: New Lands BYOD assets for Miyoo Mini.")
    parser.add_argument("data_dir", help="Path to Kingdom_Data directory or game install folder")
    parser.add_argument("--out", "-o", default=str(HERE / "sd_card_data"),
                        help="Target output folder or SD card root (default: ./sd_card_data)")
    parser.add_argument("--skip-extract", action="store_true",
                        help="Skip extraction if extracted/ already exists")
    args = parser.parse_args()

    print("==================================================================")
    print("   Kingdom: New Lands — Zero-Compile BYOD Data Preparer          ")
    print("==================================================================")

    check_dependencies()

    data_dir = find_kingdom_data(args.data_dir)
    if not data_dir:
        print(f"[!] Error: Could not locate 'Kingdom_Data' inside '{args.data_dir}'.")
        print("    Please provide the path to your installed game's Kingdom_Data folder.")
        sys.exit(1)

    print(f"[+] Found game data at: {data_dir}")

    extracted_dir = HERE / "extracted"
    pack_dir = HERE / "pack"

    # Step 1: Extract assets
    if not args.skip_extract or not extracted_dir.exists():
        print("\n[*] Step 1/3: Extracting game assets (this may take 1-2 minutes)...")
        convert_script = HERE / "tools" / "convert.py"
        cmd = [sys.executable, str(convert_script), str(data_dir), "--out", str(extracted_dir)]
        res = subprocess.run(cmd)
        if res.returncode != 0:
            print("[!] Asset extraction failed.")
            sys.exit(res.returncode)
    else:
        print("\n[*] Step 1/3: Using existing extracted/ assets...")

    # Step 2: Build compact pack
    print("\n[*] Step 2/3: Packing assets into compact 8-bit palette pack...")
    pack_script = HERE / "tools" / "pack.py"
    cmd = [sys.executable, str(pack_script), str(extracted_dir), "--out", str(pack_dir)]
    res = subprocess.run(cmd)
    if res.returncode != 0:
        print("[!] Asset packing failed.")
        sys.exit(res.returncode)

    # Step 3: Staging files for SD card
    print("\n[*] Step 3/3: Staging files for SD card...")
    out_root = Path(args.out).resolve()
    target_game_dir = out_root / "Roms" / "PORTS" / "Games" / "KingdomNewLands"
    target_data_dir = target_game_dir / "data"
    target_managed_dir = target_game_dir / "managed"

    target_data_dir.mkdir(parents=True, exist_ok=True)
    target_managed_dir.mkdir(parents=True, exist_ok=True)

    # Copy kingdom.knlpack
    knlpack_src = pack_dir / "kingdom.knlpack"
    if knlpack_src.exists():
        print(f"    • Copying kingdom.knlpack ({knlpack_src.stat().st_size / (1024*1024):.1f} MB)...")
        shutil.copy2(knlpack_src, target_data_dir / "kingdom.knlpack")

    # Copy music directory
    music_src = pack_dir / "music"
    if music_src.exists():
        print("    • Copying music files...")
        target_music = target_data_dir / "music"
        if target_music.exists():
            shutil.rmtree(target_music)
        shutil.copytree(music_src, target_music)

    # Copy user's game assemblies from Kingdom_Data/Managed
    src_managed = data_dir / "Managed"
    copied_dlls = 0
    if src_managed.is_dir():
        for dll in src_managed.glob("Assembly-CSharp*.dll"):
            print(f"    • Copying game assembly: {dll.name}...")
            shutil.copy2(dll, target_managed_dir / dll.name)
            copied_dlls += 1

    if copied_dlls == 0:
        print("[!] Warning: Could not find Assembly-CSharp.dll inside Kingdom_Data/Managed/.")

    print("\n==================================================================")
    print("   [+] DATA PREPARATION COMPLETE!                                 ")
    print("==================================================================")
    if out_root == (HERE / "sd_card_data"):
        print(f"Files have been staged to: {target_game_dir.relative_to(HERE)}")
        print("\nNext step:")
        print("1. Extract 'KingdomNewLands-MiyooMini-v1.0.zip' onto your SD card.")
        print(f"2. Copy the 'Roms' folder from '{HERE / 'sd_card_data'}' to your SD card.")
        print("3. Insert SD card into Miyoo Mini, refresh ROMs, and play!\n")
    else:
        print(f"Files have been written directly to: {target_game_dir}")
        print("\nNext step:")
        print("1. Make sure you also extracted 'KingdomNewLands-MiyooMini-v1.0.zip' to this SD card.")
        print("2. Insert SD card into Miyoo Mini, refresh ROMs, and play!\n")

if __name__ == "__main__":
    main()
