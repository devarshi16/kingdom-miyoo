@echo off
setlocal enabledelayedexpansion
title Kingdom: New Lands - Asset Preparer
cd /d "%~dp0"
cls
echo ==================================================================
echo    Kingdom: New Lands — Automated Asset Setup for Miyoo Mini
echo ==================================================================
echo.

:: 1. Check if python is installed or in known directories
where python >nul 2>nul
if %errorlevel% neq 0 (
    if exist "%LocalAppData%\Programs\Python\Python311\python.exe" (
        set "PATH=%LocalAppData%\Programs\Python\Python311;%LocalAppData%\Programs\Python\Python311\Scripts;!PATH!"
    ) else if exist "%LocalAppData%\Programs\Python\Python312\python.exe" (
        set "PATH=%LocalAppData%\Programs\Python\Python312;%LocalAppData%\Programs\Python\Python312\Scripts;!PATH!"
    ) else if exist "%ProgramFiles%\Python311\python.exe" (
        set "PATH=%ProgramFiles%\Python311;%ProgramFiles%\Python311\Scripts;!PATH!"
    ) else (
        echo [-] Python is not installed on this PC.
        echo [*] Attempting to install Python 3.11 automatically via winget...
        echo.
        winget install Python.Python.3.11 --accept-source-agreements --accept-package-agreements
        if %errorlevel% neq 0 (
            echo.
            echo [!] Automatic install failed. 
            echo     Please install Python from the Microsoft Store or python.org
            echo     (Make sure to check 'Add Python to PATH' during installation!)
            echo.
            pause
            exit /b 1
        )
        if exist "%LocalAppData%\Programs\Python\Python311\python.exe" (
            set "PATH=%LocalAppData%\Programs\Python\Python311;%LocalAppData%\Programs\Python\Python311\Scripts;!PATH!"
        ) else (
            echo.
            echo [+] Python installed successfully!
            echo [!] Please restart your computer or sign out and sign in, then run setup_windows.bat again.
            echo.
            pause
            exit /b 0
        )
    )
)

:: 2. Install required packages
echo [*] Installing/verifying required Python packages...
python -m pip install UnityPy Pillow dnfile TypeTreeGeneratorAPI --quiet

:: 3. Check for ffmpeg (needed for audio conversion)
where ffmpeg >nul 2>nul
if %errorlevel% neq 0 (
    echo [*] Attempting to install ffmpeg for audio support via winget...
    winget install Gyan.FFmpeg --accept-source-agreements --accept-package-agreements
    where ffmpeg >nul 2>nul
    if %errorlevel% neq 0 (
        echo [!] Note: ffmpeg not detected in PATH. Audio will be skipped if not present.
    )
)

:: 4. Prompt for Kingdom_Data path
set "AUTO_PATH="
if exist "C:\GOG Games\Kingdom New Lands\Kingdom_Data" set "AUTO_PATH=C:\GOG Games\Kingdom New Lands\Kingdom_Data"
if exist "C:\Program Files (x86)\Steam\steamapps\common\Kingdom New Lands\Kingdom_Data" set "AUTO_PATH=C:\Program Files (x86)\Steam\steamapps\common\Kingdom New Lands\Kingdom_Data"
if exist "C:\Program Files\Epic Games\KingdomNewLands\Kingdom_Data" set "AUTO_PATH=C:\Program Files\Epic Games\KingdomNewLands\Kingdom_Data"
if exist "%ProgramFiles(x86)%\Steam\steamapps\common\Kingdom New Lands\Kingdom_Data" set "AUTO_PATH=%ProgramFiles(x86)%\Steam\steamapps\common\Kingdom New Lands\Kingdom_Data"

echo.
echo ------------------------------------------------------------------
if defined AUTO_PATH (
    echo [+] Auto-detected Kingdom_Data at: !AUTO_PATH!
    echo Press ENTER to use this, or drag-and-drop a different Kingdom_Data folder:
) else (
    echo Please enter or drag-and-drop the path to your Kingdom_Data folder
    echo Example: C:\Program Files (x86)\Steam\steamapps\common\Kingdom New Lands\Kingdom_Data
)
echo ------------------------------------------------------------------
set /p DATA_PATH="Kingdom_Data path [press ENTER for default]: "

if "!DATA_PATH!"=="" (
    if defined AUTO_PATH (
        set "DATA_PATH=!AUTO_PATH!"
    )
)

:: Strip quotes if user dragged and dropped
set DATA_PATH=!DATA_PATH:"=!

if not exist "!DATA_PATH!" (
    echo.
    echo [!] Error: Directory '!DATA_PATH!' not found.
    pause
    exit /b 1
)

:: 4. Prompt for SD card drive letter
echo.
echo ------------------------------------------------------------------
echo Please enter your Miyoo SD card drive letter
echo Example: E:\  (or just press ENTER to save files to a local folder)
echo ------------------------------------------------------------------
set /p SD_PATH="SD card drive [default: sd_card_data]: "

if "!SD_PATH!"=="" (
    python "%~dp0prepare_data.py" "!DATA_PATH!"
) else (
    set SD_PATH=!SD_PATH:"=!
    python "%~dp0prepare_data.py" "!DATA_PATH!" --out "!SD_PATH!"
)

echo.
echo ==================================================================
echo    Setup complete! Enjoy Kingdom: New Lands on your Miyoo Mini!
echo ==================================================================
echo.
pause
