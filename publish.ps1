# publish.ps1

$releaseDir = "Release_Build"
$zipName = "DigitalWellbeing_Portable.zip"

Write-Host "Cleaning functionality..."
if (Test-Path $releaseDir) { Remove-Item -Recurse -Force $releaseDir }
if (Test-Path $zipName) { Remove-Item -Force $zipName }

New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

# Build Solutions
Write-Host "Building Release Configuration..."

# WinUI 3
Write-Host "Publishing WinUI 3 App..."
dotnet publish DigitalWellbeingWinUI3/DigitalWellbeingWinUI3.csproj -c Release -r win-x64 --self-contained false -o "$releaseDir/App"

# Service
dotnet build -c Release DigitalWellbeingService.NET4.6/DigitalWellbeingService.NET4.6.csproj
if (-not (Test-Path "$releaseDir/Service")) { New-Item -ItemType Directory -Force -Path "$releaseDir/Service" | Out-Null }
Copy-Item -Recurse "DigitalWellbeingService.NET4.6/bin/Release/net472/*" "$releaseDir/Service"

# Organize Files
Write-Host "Organizing Files..."

$finalDir = "$releaseDir/DigitalWellbeing"
New-Item -ItemType Directory -Force -Path $finalDir | Out-Null

Copy-Item -Recurse "$releaseDir/App/*" "$finalDir"
Copy-Item -Recurse "$releaseDir/Service/*" "$finalDir"


# Create Manifest / Readme
$readme = @"
DigitalWellbeing Portable
=========================

Usage:
1. Run 'DigitalWellbeingWinUI3.exe' to start the application.
2. The background service 'DigitalWellbeingService.NET4.6.exe' should start automatically or be managed by the app.

Requirements:
- .NET Framework 4.7.2
"@
Set-Content "$finalDir/README.txt" $readme

# Zip it
Write-Host "Zipping..."

# Ensure we aren't locking anything ourselves
Start-Sleep -Seconds 2

if (Test-Path $zipName) { Remove-Item -Force $zipName }

try {
    # Compress-Archive has known issues with open file handles. 
    # Using System.IO.Compression.ZipFile as a robust fallback if available, or just trying again.
    Compress-Archive -Path "$finalDir\*" -DestinationPath $zipName -Force
}
catch {
    Write-Host "Zip failed: $_"
    Write-Host "Attempting alternative..."
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory((Resolve-Path $finalDir), (Resolve-Path .).Path + "\$zipName")
}

# --- Installer Build ---
Write-Host "Building Installer..."
$setupDir = "DigitalWellbeing.Setup"
$zipSource = "$zipName"
$zipDest = "$setupDir/DigitalWellbeing_Portable.zip"

Copy-Item -Force $zipSource $zipDest

# Build Setup
dotnet build -c Release -o "$releaseDir/Setup" $setupDir/DigitalWellbeing.Setup.csproj

# Copy Output
$installerName = "DigitalWellbeing_Installer.exe"
Copy-Item -Force "$releaseDir/Setup/DigitalWellbeing.Setup.exe" $installerName

# Cleanup Intermediate Portable Artifacts
Write-Host "Cleaning up intermediate files..."
# if (Test-Path $zipName) { Remove-Item -Force $zipName }
# if (Test-Path $finalDir) { Remove-Item -Recurse -Force $finalDir }
# if (Test-Path "$releaseDir/WPF") { Remove-Item -Recurse -Force "$releaseDir/WPF" }
# if (Test-Path "$releaseDir/Service") { Remove-Item -Recurse -Force "$releaseDir/Service" }
# if (Test-Path "$releaseDir/Setup") { Remove-Item -Recurse -Force "$releaseDir/Setup" }

Write-Host "Done! Created $installerName"
