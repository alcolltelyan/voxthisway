param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = "publish/VoxThisWay",
    [string]$ZipName = "VoxThisWay-portable.zip"
)

$ErrorActionPreference = "Stop"

Write-Host "Publishing VoxThisWay.App ($Configuration, $Runtime)..." -ForegroundColor Cyan

$projectPath = "src/VoxThisWay.App/VoxThisWay.App.csproj"

# Normalize output path
$fullOutput = Join-Path (Get-Location) $OutputRoot

if (Test-Path $fullOutput) {
    Write-Host "Cleaning existing output at $fullOutput" -ForegroundColor Yellow
    Remove-Item -Recurse -Force $fullOutput
}

& dotnet publish $projectPath -c $Configuration -r $Runtime --self-contained:$false -o $fullOutput

Write-Host "Publish completed to $fullOutput" -ForegroundColor Green

# Ensure Speech folder exists or at least remind the caller.
$speechPath = Join-Path $fullOutput "Speech"
if (-not (Test-Path $speechPath)) {
    Write-Warning "Speech folder not found in publish output. Remember to copy your Speech\\ folder (whisper_cli.exe + Models/) next to VoxThisWay.App.exe before distributing."
}

# Create ZIP next to the publish root
$publishRoot = Split-Path $fullOutput -Parent
$zipPath = Join-Path $publishRoot $ZipName

if (Test-Path $zipPath) {
    Write-Host "Removing existing ZIP at $zipPath" -ForegroundColor Yellow
    Remove-Item $zipPath -Force
}

Write-Host "Creating ZIP archive $zipPath..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $fullOutput '*') -DestinationPath $zipPath

Write-Host "Done. Portable ZIP created at: $zipPath" -ForegroundColor Green
