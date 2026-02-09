@echo off
TITLE EyeGestures Unity Bridge
ECHO ========================================================
ECHO    EyeGestures to Unity Bridge - Launcher
ECHO ========================================================
ECHO.

:: Check if Python is installed
python --version >nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    ECHO [ERROR] Python is not found! 
    ECHO Please install Python 3.8+ and add it to your PATH.
    PAUSE
    EXIT /B
)

:: Set local library path (next to this script)
SET LIB_DIR=%~dp0lib

:: Check if eyeGestures is installed locally
IF NOT EXIST "%LIB_DIR%\eyeGestures" (
    ECHO [INFO] EyeGestures not found in local lib folder.
    ECHO [INFO] Installing from GitHub into %LIB_DIR%...
    ECHO.
    pip install --target "%LIB_DIR%" git+https://github.com/NativeSensors/EyeGestures.git
    IF %ERRORLEVEL% NEQ 0 (
        ECHO [ERROR] Failed to install EyeGestures from GitHub.
        ECHO [ERROR] Check your internet connection and try again.
        PAUSE
        EXIT /B
    )
    ECHO [OK] EyeGestures installed successfully.
) ELSE (
    ECHO [OK] EyeGestures found in local lib.
)

:: Run the bridge script
ECHO.
ECHO Starting Eye Tracker...
ECHO Keep this window OPEN while running Unity.
ECHO.
python "%~dp0eyeGestures_to_unity.py"

PAUSE
