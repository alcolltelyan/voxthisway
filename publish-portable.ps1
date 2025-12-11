param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = "publish/VoxThisWay",
    [string]$ZipName = "VoxThisWay-portable.zip",
    # Optional source folder containing Speech\ (whisper_cli.exe + Models/).
    # By default this is resolved to a top-level Speech folder next to this script.
    [string]$SpeechSourceRoot = "Speech"
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

# Optionally copy Speech folder into publish output.
$repoRoot = Get-Location
$speechSource = if ([System.IO.Path]::IsPathRooted($SpeechSourceRoot)) {
    $SpeechSourceRoot
} else {
    Join-Path $repoRoot $SpeechSourceRoot
}

if (Test-Path $speechSource) {
    $speechTarget = Join-Path $fullOutput "Speech"

    Write-Host "Copying Speech folder from '$speechSource' to '$speechTarget'..." -ForegroundColor Cyan

    if (Test-Path $speechTarget) {
        Remove-Item -Recurse -Force $speechTarget
    }

    # Preserve the Speech folder structure (including Models/) under the publish root.
    Copy-Item -Path $speechSource -Destination $fullOutput -Recurse -Container
} else {
    Write-Warning "Speech source folder '$speechSource' not found. Remember to copy your Speech\\ folder (whisper_cli.exe + Models/) next to VoxThisWay.App.exe before distributing."
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
