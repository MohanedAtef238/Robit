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

:: Check if required packages are installed
ECHO Checking dependencies...
pip show eyeGestures >nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    ECHO [INFO] Installing required libraries...
    pip install eyeGestures opencv-contrib-python numpy
    IF %ERRORLEVEL% NEQ 0 (
        ECHO [ERROR] Failed to install dependencies.
        PAUSE
        EXIT /B
    )
) ELSE (
    ECHO [OK] Dependencies found.
)

:: Run the bridge script
ECHO.
ECHO Starting Eye Tracker...
ECHO Keep this window OPEN while running Unity.
ECHO.
python eyeGestures_to_unity.py

PAUSE
