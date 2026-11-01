#!/usr/bin/env python3
"""Folder-picker frontend; conversion runs in a separate process with a persistent log."""
import os
from pathlib import Path
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent


def main():
    try:
        import tkinter as tk
        from tkinter import filedialog, messagebox
    except ImportError:
        print("Folder picker requires Tkinter (on Ubuntu: python3-tk). CLI alternative:")
        print('python prepare_data.py "/path/to/Kingdom_Data" --out "/path/to/SD"')
        return 1
    root = tk.Tk()
    root.withdraw()
    messagebox.showinfo("Experimental candidate", "This candidate still needs full gameplay testing. Existing installations and saves will be backed up.\n\nSelect your owned Kingdom_Data folder, then your SD card root or a local output folder.")
    data = filedialog.askdirectory(title="Select Kingdom_Data (or its parent game folder)")
    if not data:
        return 1
    output = filedialog.askdirectory(title="Select the SD card root or a local output folder")
    if not output:
        return 1
    command = [sys.executable, str(HERE / "prepare_data.py"), data, "--out", output]
    if not shutil.which("ffmpeg"):
        if not messagebox.askyesno("FFmpeg missing", "FFmpeg is needed for sound. Create a SILENT test installation instead?\n\nChoose No to install FFmpeg and try again."):
            return 1
        command.append("--skip-audio")
    root.deiconify()
    root.title("Kingdom New Lands - Preparing data")
    label = tk.Label(root, text="Preparing and validating game data…\nProgress appears in the terminal. Please keep this window open.", padx=24, pady=24)
    label.pack()
    process = subprocess.Popen(command, cwd=HERE)
    root.protocol("WM_DELETE_WINDOW", lambda: messagebox.showinfo("Preparation running", "Please wait for preparation to finish. See the terminal for progress."))
    result = [1]
    def poll():
        status = process.poll()
        if status is None:
            root.after(300, poll)
            return
        result[0] = status
        if status == 0:
            messagebox.showinfo("Candidate installed", "Preparation passed. This is an experimental candidate; keep the backup until gameplay and save/reload testing pass.")
        else:
            messagebox.showerror("Preparation failed", "Installation did not complete. See prepare.log beside this setup program. The previous installation is preserved.")
        root.destroy()
    poll()
    root.mainloop()
    return result[0]


if __name__ == "__main__":
    sys.exit(main())
