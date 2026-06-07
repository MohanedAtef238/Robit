$ErrorActionPreference = "Stop"

$GazeDir = "D:\Projects\neo gazefollower\GazeFollower-main"
$EmgDir  = "C:\Users\Mohaned\Downloads\robit_stuff\emg-work-main\emg-work-main"
$OutDir  = $PSScriptRoot

# ── Gaze Bridge ──────────────────────────────────────────────────────────────
# MUST use the project .venv — it contains mediapipe 0.10.10 with the
# face_landmark model files (.binarypb / .tflite) that PyInstaller bundles.
# The global Python313 does NOT have these files and will produce a broken EXE.
$GazeVenvPython = "$GazeDir\.venv\Scripts\python.exe"
if (-not (Test-Path $GazeVenvPython)) {
    Write-Error "Gaze .venv not found at '$GazeVenvPython'. Run: python -m venv .venv && .venv\Scripts\pip install -r requirements.txt"
}

Write-Host "Building Gaze Bridge (using .venv)..." -ForegroundColor Cyan
Set-Location $GazeDir

# Use the .spec file — it sets datas=collect_data_files('mediapipe') and
# hiddenimports=['SharedMemoryCamera']. Do NOT use --onefile directly as
# it skips the spec and produces a broken EXE missing the model files.
& $GazeVenvPython -m PyInstaller unity_gaze_bridge.spec --distpath $OutDir --noconfirm

# ── EMG Bridge ───────────────────────────────────────────────────────────────
Write-Host "Building EMG Bridge..." -ForegroundColor Cyan
Set-Location $EmgDir
python -m PyInstaller --onefile unity_emg_bridge.py --distpath $OutDir --name unity_emg_bridge --noconfirm

Write-Host "Builds complete! Executables are in: $OutDir" -ForegroundColor Green
