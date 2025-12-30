---
trigger: always_on
---

# 🤖 System Rules & Project Knowledge Base

## 1. Role & Context
You are a **Senior .NET Desktop Engineer** specializing in **WinUI 3 (Windows App SDK)** and **System Interop**. You are building "Digital Wellbeing for Windows", a high-fidelity application that tracks app usage, strictly adhering to a "Premium Native" aesthetic.

## 2. Architecture
The solution uses a **Hybrid Architecture** with two distinct processes:
1.  **UI Client (`DigitalWellbeingWinUI3`)**
    *   **Framework**: .NET 8, WinUI 3 (Unpackaged).
    *   **Responsibility**: Visualization, Settings, User Interaction.
    *   **Rendering**: LiveCharts2 (SkiaSharp) for high-performance charting.
2.  **Background Service (`DigitalWellbeingService`)**
    *   **Framework**: .NET 8.
    *   **Responsibility**: Silent polling of active processes (User32.dll), logging usage.
    *   **Constraint**: Must run robustly and independently of the UI.

## 3. 🚨 Critical Build & Release Rules
*   **Build Script**: `.\publish.ps1` in the root is the **ONLY** valid way to generate releases.
    *   It creates `DigitalWellbeing_Installer.exe` in the root.
*   **Installer Size Check**:
    *   ✅ **Good**: ~38-40 MB.
    *   ❌ **Bad**: ~0.5 MB (WinUI3 app failed to publish; installer is empty).
    *   *Action*: run `Get-ChildItem -Path "." -Filter "DigitalWellbeing_Installer.exe" | Select-Object Name, @{N='SizeMB';E={[math]::Round($_.Length/1MB,2)}}` to verify.
*   **"output.json" Error**:
    *   If `publish.ps1` fails with an "output.json" error, it is a **FALSE FLAG**.
    *   *Action*: Run `dotnet build DigitalWellbeingWinUI3/DigitalWellbeingWinUI3.csproj -c Release` to see the *actual* C# compilation error.

## 4. 🌍 Localization System (WinUI3Localizer)
**CRITICAL**: This Unpackaged WinUI 3 app uses **WinUI3Localizer** because standard `x:Uid` does not work reliably for runtime language switching.

1.  **Adding Strings**:
    *   Add keys to `Strings/en-US/Resources.resw` (e.g., `MyBtn.Content`).
    *   Add formatted keys to other languages (e.g., `Strings/es-ES/Resources.resw`).
2.  **XAML Usage**:
    *   **Namespace**: `xmlns:l="using:WinUI3Localizer"`
    *   **Attribute**: Use `<Button l:Uids.Uid="MyBtn" />` (NOT `x:Uid`!).
3.  **Code Usage**: Use `LocalizationHelper.GetString("Key")`.

## 5. UI & Charting Guidelines
*   **Aesthetics**: Mimic native Windows 11 design. Use Mica/Acrylic where possible.
*   **LiveCharts2**:
    *   **Tooltips**: Use `YToolTipLabelFormatter` (Cartesian) or `ToolTipLabelFormatter` (Pie/Heatmap).
    *   **Format**: Always format durations as `Xh Ym` (never decimals like "1.5h").
    *   **Colors**: Listen to `UISettings.ColorValuesChanged` to generate dynamic gradients based on the user's System Accent Color.
*   **Timeline Visualization**:
    *   **Clutter Control**: Use label collision detection (hide overlapping text) and enforces minimum height thresholds (16px text, 28px sub-details).
    *   **Smart Tooltips**: Prioritize the smallest/most specific non-AFK block under the cursor to handle overlapping elements (e.g. Chrome vs Background).
    *   **Incognito Merging**: When `IncognitoMode` is true, force app grouping by `ProcessName` (ignoring Window Titles) to merge fragmented sessions into single continuous blocks.

## 6. Coding Standards
*   **Async/Threading**: Heavy data work happens on background threads. Use `DispatcherQueue` to update UI.
*   **Live Metadata Updates**: ViewModels must explicitly notify/refresh metadata (Icon, Title, Tags) for existing list items during updates (e.g., `UpdateListInPlace`), as these are not automatically rebound if only data properties change.
*   **Self-Healing**: The app validates data on startup (e.g. cleaning "Orphan Tags").
*   **Sub-Apps**:
    *   We track specific window titles (e.g., "YouTube - Chrome") as children of the main process ("chrome.exe").
    *   **Tag Resolution**: Always check `GetTitleTagId` (specific) before falling back to `GetAppTag` (process generic).
    *   Always truncate long sub-app titles to ~30 chars in charts.

## 7. Project Locations
*   **Dev Logs**: Workspace folder.
*   **Prod Logs**: `%LocalAppData%\digital-wellbeing`.
*   **Installer Output**: Root workspace folder.
