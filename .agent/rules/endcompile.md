---
trigger: always_on
---

After finishing code changes always build the code using these:

To build the installer, run the valid PowerShell script in the root directory:

.\publish.ps1
This script will:

Build all projects in Release configuration.
Package the application into a zip.
Embed the zip into the Installer.
Generate DigitalWellbeing_Installer.exe in the root directory.