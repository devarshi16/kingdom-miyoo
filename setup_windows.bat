@echo off
setlocal
cd /d "%~dp0"
title Kingdom New Lands - Experimental Candidate Setup
set "PYTHONUTF8=1"
set "PYTHON="
py -3.11 -c "import sys; assert sys.version_info >= (3,10)" >nul 2>nul
if not errorlevel 1 set "PYTHON=py -3.11"
if defined PYTHON goto found_python
python -c "import sys; assert (3,10) <= sys.version_info[:2] <= (3,12)" >nul 2>nul
if not errorlevel 1 set "PYTHON=python"
if defined PYTHON goto found_python
echo Install Python 3.11 from python.org, then run this file again.
echo Enable Python's launcher and Add Python to PATH during installation.
pause
exit /b 1
:found_python
if not exist ".player-venv\Scripts\python.exe" %PYTHON% -m venv .player-venv
if errorlevel 1 goto failed
.player-venv\Scripts\python.exe -m pip install -r tools\requirements.txt
if errorlevel 1 goto failed
if exist "tools\ffmpeg\ffmpeg.exe" set "PATH=%CD%\tools\ffmpeg;%PATH%"
.player-venv\Scripts\python.exe setup_gui.py
if errorlevel 1 goto failed
exit /b 0
:failed
echo Setup failed. Please share prepare.log or the error shown above.
pause
exit /b 1
