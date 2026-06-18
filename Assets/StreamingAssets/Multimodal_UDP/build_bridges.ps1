# build_bridges.ps1

# This script builds the python bridges for the Robit project.
# It iterates through the subdirectories of the Repos folder,
# installs dependencies from requirements.txt, and then (as a placeholder)
# would run the build script for each bridge.

$ErrorActionPreference = "Stop"

$reposDir = "d:\Projects\Robit\Repos"

# Get all subdirectories in the Repos directory
$bridgeDirs = Get-ChildItem -Path $reposDir -Directory

foreach ($bridgeDir in $bridgeDirs) {
    $projectPath = $bridgeDir.FullName
    Write-Host "Processing bridge in: $projectPath"

    # Set location to the bridge directory
    Push-Location -Path $projectPath

    # Check for python files before proceeding
    $pythonFiles = Get-ChildItem -Path . -Filter *.py -Recurse
    if ($pythonFiles.Count -eq 0) {
        Write-Host "No python files found, skipping folder: $projectPath"
        Pop-Location
        continue
    }

    # Check for requirements.txt
    if (-not (Test-Path "requirements.txt" -PathType Leaf)) {
        Write-Host "'requirements.txt' not found. Generating it using pipreqs."

        # Check if pipreqs is installed
        $pipreqsCheck = pip list | findstr "pipreqs"
        if ($LASTEXITCODE -ne 0) {
            Write-Host "pipreqs not found, installing..."
            pip install pipreqs
        }

        # Generate requirements.txt
        # Using --force to overwrite any existing (even though we checked)
        # We point it to the current directory '.'
        pipreqs . --force
        Write-Host "'requirements.txt' generated."
    }

    # Install dependencies
    Write-Host "Installing dependencies from requirements.txt..."
    pip install -r requirements.txt

    # Placeholder for running the actual python build/run script
    Write-Host "Dependencies installed. Ready to run the bridge."
    # Example: python main.py
    # (Your actual script to run may vary)

    # Return to the original directory
    Pop-Location
}

Write-Host "All bridges processed."
