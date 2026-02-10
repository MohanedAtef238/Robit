@ECHO OFF
SETLOCAL EnableDelayedExpansion
TITLE EyeGestures to Unity Bridge

:: ─── Configuration ───
SET "SCRIPT_DIR=%~dp0"
SET "VENV_DIR=%SCRIPT_DIR%.venv"
SET "VENV_PYTHON=%VENV_DIR%\Scripts\python.exe"
SET "VENV_PIP=%VENV_DIR%\Scripts\pip.exe"
SET "REQ_FILE=%SCRIPT_DIR%requirements.txt"
SET "BRIDGE=%SCRIPT_DIR%eyeGestures_to_unity.py"
SET "GITHUB_REPO=git+https://github.com/NativeSensors/EyeGestures.git"

:: ─── Step 1: Ensure Python 3.11 is available ───
ECHO [1/4] Checking for Python 3.11...
py -3.11 --version >nul 2>&1
IF %ERRORLEVEL% NEQ 0 GOTO InstallPython

FOR /F "tokens=*" %%V IN ('py -3.11 --version 2^>^&1') DO ECHO [OK] Found %%V
GOTO CheckVenv

:InstallPython
ECHO [INFO] Python 3.11 not found. Installing via winget...
winget install Python.Python.3.11 --silent --accept-package-agreements --accept-source-agreements
IF %ERRORLEVEL% NEQ 0 (
    ECHO [ERROR] Failed to install Python 3.11.
    ECHO [ERROR] Please install manually from https://python.org/downloads/
    PAUSE
    EXIT /B 1
)
ECHO [OK] Python 3.11 installed. You may need to restart this script.
ECHO     Close this window and double-click run_eye_tracker.bat again.
PAUSE
EXIT /B 0

:: ─── Step 2: Create venv if needed ───
:CheckVenv
IF EXIST "%VENV_PYTHON%" (
    ECHO [2/4] Virtual environment already exists.
    GOTO CheckDeps
)

ECHO.
ECHO [2/4] Creating virtual environment...
py -3.11 -m venv "%VENV_DIR%"
IF %ERRORLEVEL% NEQ 0 (
    ECHO [ERROR] Failed to create virtual environment.
    PAUSE
    EXIT /B 1
)
ECHO [OK] Virtual environment created at .venv\

:: ─── Step 3: Install dependencies ───
:CheckDeps
"%VENV_PYTHON%" -c "from eyeGestures import EyeGestures_v3" >nul 2>&1
IF %ERRORLEVEL% EQU 0 (
    ECHO [3/4] Dependencies already installed.
    GOTO RunBridge
)

ECHO.
ECHO [3/4] Installing dependencies (first run, may take a minute)...

:: Upgrade pip first
"%VENV_PYTHON%" -m pip install --upgrade pip >nul 2>&1

:: Install EyeGestures from GitHub
ECHO       Installing EyeGestures from GitHub...
"%VENV_PIP%" install "%GITHUB_REPO%"
IF %ERRORLEVEL% NEQ 0 (
    ECHO [ERROR] Failed to install EyeGestures.
    PAUSE
    EXIT /B 1
)

:: Install remaining requirements
IF EXIST "%REQ_FILE%" (
    ECHO       Installing requirements...
    "%VENV_PIP%" install -r "%REQ_FILE%"
    IF %ERRORLEVEL% NEQ 0 (
        ECHO [WARNING] Some optional packages failed to install.
    )
)

:: Verify
"%VENV_PYTHON%" -c "from eyeGestures import EyeGestures_v3; print('[OK] EyeGestures verified')"
IF %ERRORLEVEL% NEQ 0 (
    ECHO [ERROR] EyeGestures verification failed.
    PAUSE
    EXIT /B 1
)

:: ─── Step 4: Run the bridge ───
:RunBridge
ECHO.
ECHO [4/4] Starting Eye Tracker Bridge...
ECHO       Keep this window OPEN while running Unity.
ECHO       Press Ctrl+C or 'q' to stop.
ECHO ----------------------------------------------
ECHO.

"%VENV_PYTHON%" "%BRIDGE%" %*

ECHO.
ECHO [Bridge] Stopped.
PAUSE
