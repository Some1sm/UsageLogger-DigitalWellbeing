# publish.ps1
param(
    [switch]$AppOnly,
    [switch]$NoZip
)

$ErrorActionPreference = "Stop"
$releaseDir = "Release_Build"
$finalDir = "$releaseDir/UsageLogger"
$zipName = "UsageLogger_Portable.zip"
$installerName = "UsageLogger_Installer.exe"
$setupDir = "UsageLogger.Setup"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   UsageLogger Release Publish Pipeline   " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Clean previous build artifacts
Write-Host "[1/5] Cleaning output directories..." -ForegroundColor Yellow
if (Test-Path $releaseDir) { Remove-Item -Recurse -Force $releaseDir }
if (Test-Path $zipName) { Remove-Item -Force $zipName }
if (Test-Path "$setupDir/$zipName") { Remove-Item -Force "$setupDir/$zipName" }

New-Item -ItemType Directory -Force -Path $finalDir | Out-Null

# 2. Publish Main App & Service
Write-Host "[2/5] Publishing WinUI 3 App and Background Service..." -ForegroundColor Yellow
dotnet publish UsageLogger/UsageLogger.csproj -c Release -r win-x64 --self-contained false -o "$releaseDir/App" -v q
dotnet publish UsageLoggerService/UsageLoggerService.csproj -c Release -r win-x64 --self-contained false -o "$releaseDir/Service" -v q

# 3. Organize into final directory
Write-Host "[3/5] Organizing distribution binaries..." -ForegroundColor Yellow
$excludeList = @("*.pdb", "*.xml", "WindowsAppRuntime.png", "Microsoft.Web.WebView2.*", "WebView2Loader.*", "UsageLoggerService.dll.config")
Copy-Item -Recurse "$releaseDir/App/*" "$finalDir" -Exclude $excludeList
Copy-Item -Recurse "$releaseDir/Service/*" "$finalDir" -Exclude $excludeList

# Clean up unwanted framework language folders
$keepLangs = @("en-US", "es-ES", "ca-ES", "fr", "fr-FR", "de", "de-DE", "it", "it-IT", "pt-BR", "ru", "ru-RU", "ja", "ja-JP", "ko", "ko-KR", "zh-Hans", "zh-CN", "Strings", "Assets", "Microsoft.UI.Xaml")
Get-ChildItem -Path $finalDir -Directory | ForEach-Object {
    if ($keepLangs -notcontains $_.Name) {
        Remove-Item -Recurse -Force $_.FullName
    }
}

# Clean intermediate App and Service output
Remove-Item -Recurse -Force "$releaseDir/App"
Remove-Item -Recurse -Force "$releaseDir/Service"

Write-Host "Direct runnable binaries ready at: $finalDir\UsageLogger.exe" -ForegroundColor Green

if ($AppOnly) {
    Write-Host "`nApp-only build complete! Run: $finalDir\UsageLogger.exe" -ForegroundColor Green
    exit 0
}

# 4. Build Uninstaller directly into distribution folder
Write-Host "[4/5] Building lightweight Uninstaller..." -ForegroundColor Yellow
dotnet build -c Release -p:EmbedPayload=false -o "$releaseDir/Uninstaller" $setupDir/UsageLogger.Setup.csproj -v q
Copy-Item -Force "$releaseDir/Uninstaller/UsageLogger.Setup.exe" "$finalDir/Uninstall.exe"
Remove-Item -Recurse -Force "$releaseDir/Uninstaller"

# 5. Compress once directly to Portable Zip & Installer Payload
Write-Host "[5/5] Packaging and building Installer..." -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem

[System.IO.Compression.ZipFile]::CreateFromDirectory(
    (Resolve-Path $finalDir).Path,
    (Join-Path (Get-Location) $zipName),
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false
)

# Copy zip payload for the installer embedding
Copy-Item -Force $zipName "$setupDir/$zipName"

# Build Full Installer (With Payload)
dotnet build -c Release -o "$releaseDir/Setup" $setupDir/UsageLogger.Setup.csproj -v q
Copy-Item -Force "$releaseDir/Setup/UsageLogger.Setup.exe" $installerName
Remove-Item -Recurse -Force "$releaseDir/Setup"
if (Test-Path "$setupDir/$zipName") { Remove-Item -Force "$setupDir/$zipName" }

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host "Build Succeeded!" -ForegroundColor Green
Write-Host "  - Direct App:  $finalDir\UsageLogger.exe" -ForegroundColor White
Write-Host "  - Portable:    $zipName" -ForegroundColor White
Write-Host "  - Installer:   $installerName" -ForegroundColor White
Write-Host "==========================================" -ForegroundColor Green
