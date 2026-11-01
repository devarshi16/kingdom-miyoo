@echo off
setlocal enabledelayedexpansion
title Kingdom: New Lands - Asset Preparer
cls
echo ==================================================================
echo    Kingdom: New Lands — Automated Asset Setup for Miyoo Mini
echo ==================================================================
echo.

:: 1. Check if python is installed
where python >nul 2>nul
if %errorlevel% neq 0 (
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
    echo.
    echo [+] Python installed successfully!
    echo [!] Please close this window and double-click setup_windows.bat again.
    echo.
    pause
    exit /b 0
)

:: 2. Install required packages
echo [*] Installing/verifying required packages...
python -m pip install UnityPy Pillow dnfile TypeTreeGeneratorAPI --quiet

:: 3. Prompt for Kingdom_Data path
echo.
echo ------------------------------------------------------------------
echo Please enter or drag-and-drop the path to your Kingdom_Data folder
echo Example: C:\Program Files (x86)\Steam\steamapps\common\Kingdom New Lands\Kingdom_Data
echo ------------------------------------------------------------------
set /p DATA_PATH="Kingdom_Data path: "

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
    python prepare_data.py "!DATA_PATH!"
) else (
    set SD_PATH=!SD_PATH:"=!
    python prepare_data.py "!DATA_PATH!" --out "!SD_PATH!"
)

echo.
echo ==================================================================
echo    Setup complete! Enjoy Kingdom: New Lands on your Miyoo Mini!
echo ==================================================================
echo.
pause
