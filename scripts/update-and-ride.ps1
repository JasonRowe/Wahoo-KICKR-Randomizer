# Fetch the latest nightly build of BikeFitnessApp (Windows/WPF), install, and launch.
$ErrorActionPreference = "Stop"

$Repo = "JasonRowe/Wahoo-KICKR-Randomizer"
$Asset = "bikefitness-win-x64.zip"
$Exe = "BikeFitnessApp.exe"
$InstallDir = if ($env:BIKEFITNESS_INSTALL_DIR) { $env:BIKEFITNESS_INSTALL_DIR } else { Join-Path $env:LOCALAPPDATA "BikeFitnessApp" }
$Url = "https://github.com/$Repo/releases/download/nightly/$Asset"

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
$tmp = Join-Path $env:TEMP ("bikefitness-" + [guid]::NewGuid().ToString() + ".zip")

Write-Host "Downloading $Asset ..."
Invoke-WebRequest -Uri $Url -OutFile $tmp -UseBasicParsing

Write-Host "Installing to $InstallDir ..."
Expand-Archive -Path $tmp -DestinationPath $InstallDir -Force

Remove-Item -Path $tmp -Force

Write-Host "Launching $Exe ..."
Start-Process -FilePath (Join-Path $InstallDir $Exe)
