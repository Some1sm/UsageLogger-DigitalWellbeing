# Windows Digital Wellbeing

A modern App Usage tracker (time tracker) for Windows 11 inspired by Digital Wellbeing in Android. 
Built with **WinUI 3 / Windows App SDK** for a beautiful, native Windows 11 look and feel.

## Features
- **Weekly Usage**: View usage trends for the past 7 days.
- **Day App Usage**: Detailed daily breakdown with Pie Charts and Lists.
- **Sub-App Tracking**: Track time for specific window titles (e.g., individual browser tabs, sub-windows) separately.
- **Time Limits**: Set time limits per app or sub-app.
    - **Enforcement**: receive notifications and optional blocking prompts (Ignore, +5min, Close App) when limits are exceeded.
- **App Tagging**: Organize apps into categories (Work, Social, Games, etc.) with custom colors.
    - **Sub-Tagging**: Assign different categories to specific sub-apps (e.g., "YouTube" as *Entertainment* vs "Docs" as *Work* within the same browser).
- **Auto-Refresh**: Live UI updates including duration counters.
- **Exclude Apps**: Filter out specific apps or helper processes.
- **Incognito Mode**: Option to pause detailed tracking.
- **Auto-Start**: Can run on startup, minimized to system tray.
- **Design**: Fully supports Light/Dark themes and uses Windows 11 Mica materials.

## Installation
**Download the .exe** installer from the [Releases](https://github.com/Some1sm/Windows-Digital-Wellbeing/releases) page (coming soon).

## Troubleshooting

### App crashing when opened
If the app crashes upon opening, try:
1. Uninstall.
2. Delete the contents of `%LOCALAPPDATA%/digital-wellbeing/`
3. Re-install the latest version.

## For Developers

### Solution Projects
- `DigitalWellbeing.Core` - Static shared class library.
- `DigitalWellbeingService` - Background service (Core tracking logic, .NET Framework 4.7.2).
- `DigitalWellbeingWinUI3` - Modern Frontend (.NET 8, WinUI 3).
- `DigitalWellbeing.Setup` - Installer generation logic.

### How to Build
To build the installer, run the valid PowerShell script in the root directory:
```powershell
.\publish.ps1
```
This script will:
1.  Build all projects in `Release` configuration.
2.  Package the application into a zip.
3.  Embed the zip into the Installer.
4.  Generate `DigitalWellbeing_Installer.exe` in the root directory.
