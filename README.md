# Windows Digital Wellbeing

An App Usage tracker (time tracker) for Windows 11 inspired by Digital Wellbeing in Android. 
Recently ported to **WinUI 3 / Windows App SDK** for a modern, native Windows 11 look and feel.

> **Note**: This is a fork/continuation of the original project, now rewritten for better performance and aesthetics.

## Features
- **Weekly Usage**. View past week's total usage time (last 7 days).
- **Day App Usage**. View daily app usage time (Pie Chart and List).
- **Alert Notifications**. Set a time limit per app of when to notify you when limit is exceeded.
- **Auto-Start**. Run on Startup option, minimized to tray.
- **App Tagging**. Tag apps based on their category.
- **Exclude Apps**. Set a filter of apps to exclude.
- **Filter out short time entries**. Set a filter to hide apps that are run less than the set time limit.
- **Auto-Refresh**. Auto-Refresh charts on intervals.
- **Design**. Full Dark Mode support and Windows 11 Mica material.

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
- `DigitalWellbeing.Core` - A class library that has static shared classes.
- `DigitalWellbeingService.NET4.6` - Background service (Legacy .NET 4.6 for compatibility).
- `DigitalWellbeingWinUI3` - **New** WinUI 3 Frontend.
- `DigitalWellbeingWPF` - **Legacy** WPF Frontend (Deprecaated).
- `DigitalWellbeing.Setup` - Installer logic.

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
