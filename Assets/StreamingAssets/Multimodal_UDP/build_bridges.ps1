$ErrorActionPreference = "Stop"

$GazeDir = "C:\Users\Mohaned\Downloads\robit_stuff\GazeFollower-main\GazeFollower-main"
$EmgDir = "C:\Users\Mohaned\Downloads\robit_stuff\emg-work-main\emg-work-main"
$OutDir = $PSScriptRoot

Write-Host "Make sure you have installed pyinstaller: pip install pyinstaller" -ForegroundColor Yellow

Write-Host "Building Gaze Bridge..." -ForegroundColor Cyan
Set-Location $GazeDir
python -m PyInstaller --onefile unity_gaze_bridge.py --distpath $OutDir --name unity_gaze_bridge

Write-Host "Building EMG Bridge..." -ForegroundColor Cyan
Set-Location $EmgDir
python -m PyInstaller --onefile unity_emg_bridge.py --distpath $OutDir --name unity_emg_bridge

Write-Host "Builds Complete! Executables are located in $OutDir" -ForegroundColor Green
