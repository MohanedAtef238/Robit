$ErrorActionPreference = "Stop"

$OutDir = $PSScriptRoot
$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$ReposDir = Join-Path $ProjectRoot "Repos"
$OriginalLocation = Get-Location

$Targets = @(
    @{
        Name = "share_camera"
        ProjectDir = Join-Path $ReposDir "GazeFollower"
        Spec = "share_camera.spec"
    },
    @{
        Name = "unity_gaze_bridge"
        ProjectDir = Join-Path $ReposDir "GazeFollower"
        Spec = "unity_gaze_bridge.spec"
    },
    @{
        Name = "unity_gestures_bridge"
        ProjectDir = Join-Path $ReposDir "face_gestures"
        Spec = "unity_gestures_bridge.spec"
    },
    @{
        Name = "unity_emg_bridge"
        ProjectDir = Join-Path $ReposDir "EMG"
        Spec = "unity_emg_bridge.spec"
    }
)

function Get-BridgePython {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectDir,
        [Parameter(Mandatory = $true)][string]$BridgeName
    )

    $venvPython = Join-Path $ProjectDir ".venv\Scripts\python.exe"
    if (Test-Path $venvPython) {
        return $venvPython
    }

    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    if ($null -eq $pythonCommand) {
        throw "No Python found for $BridgeName. Create '$ProjectDir\.venv' or add python.exe to PATH."
    }

    return $pythonCommand.Source
}

try {
    foreach ($target in $Targets) {
        $name = $target.Name
        $projectDir = $target.ProjectDir
        $specPath = Join-Path $projectDir $target.Spec

        if (-not (Test-Path $projectDir)) {
            throw "Project directory not found for ${name}: $projectDir"
        }

        if (-not (Test-Path $specPath)) {
            throw "PyInstaller spec not found for ${name}: $specPath"
        }

        $python = Get-BridgePython -ProjectDir $projectDir -BridgeName $name

        Write-Host "Handling dependencies for $name..."
        $requirementsPath = Join-Path $projectDir "requirements.txt"
        if (-not (Test-Path $requirementsPath)) {
            Write-Host "requirements.txt not found for $name. Generating with pipreqs..."
            & $python -m pip install --quiet pipreqs
            & $python -m pipreqs --encoding utf-8 --force $projectDir --savepath $requirementsPath
            Write-Host "requirements.txt generated."
        }

        Write-Host "Installing dependencies for $name from requirements.txt..."
        & $python -m pip install -r $requirementsPath

        Write-Host "Building $name..." -ForegroundColor Cyan
        Set-Location $projectDir
        & $python -m PyInstaller $specPath --distpath $OutDir --noconfirm

        if ($LASTEXITCODE -ne 0) {
            throw "PyInstaller failed for $name with exit code $LASTEXITCODE."
        }

        $exePath = Join-Path $OutDir "$name\$name.exe"
        if (-not (Test-Path $exePath)) {
            throw "Expected executable was not produced: $exePath"
        }
    }
}
finally {
    Set-Location $OriginalLocation
}

Write-Host "Builds complete. Executables are in: $OutDir" -ForegroundColor Green
