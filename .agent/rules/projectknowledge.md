---
trigger: always_on
---

# Project Knowledge Base & AI Rules

## 1. 🚨 Critical Build & Troubleshooting Rules

### The "output.json" False Flag
**Symptom:** The `publish.ps1` script fails, or `dotnet publish` fails with an error referencing `output.json` (e.g., `...win-x64\output.json' exited with code 1`).
**Reality:** This is often a **red herring** masking a standard C# compilation error.
**Fix:**
1.  **Do NOT** chase MSIX/packaging configurations immediately.
2.  **DO** run a standard build command to unmask the true error:
    ```powershell
    dotnet build DigitalWellbeingWinUI3/DigitalWellbeingWinUI3.csproj -c Release
    ```
3.  Fix the C# syntax error (e.g., missing property, wrong type) revealed by this command.

### Installer Size Checks
- **Good Build:** ~38-40 MB.
- **Bad Build:** ~0.5 MB (Indicates the WinUI 3 app failed to publish, leaving the installer container empty).
- **Rule:** Always verify the installer size after a build using:
  ```powershell
  Get-ChildItem -Path "." -Filter "DigitalWellbeing_Installer.exe" | Select-Object Name, @{N='SizeMB';E={[math]::Round($_.Length/1MB,2)}}
  ```

---

## 2. 📊 LiveCharts2 Implementation Rules

**Property Names Matter:**
- **Cartesian Charts (Column, Line, Row):** Use `YToolTipLabelFormatter` (or `XToolTipLabelFormatter`).
  - *Incorrect:* `TooltipLabelFormatter` (Will cause build failure).
- **Pie Charts & Heatmaps:** Use `ToolTipLabelFormatter`.

**Formatting Standard:**
- Always use the `FormatHours` or `FormatMinutes` helpers to display time as `Xh Ym Zs` or `HH:MM:SS`.
- Avoid raw decimals (e.g., "8.7 hours") in tooltips.

---

## 3. 📖 Project Terminology & Architecture

### Application Hierarchy
1.  **Process (Parent App):** The main executable name (e.g., `chrome.exe`, `spotify.exe`).
    - *Code Ref:* `AppUsage.ProcessName`
    - *Display:* "Google Chrome", "Spotify" (via `UserPreferences.GetDisplayName`)

2.  **Sub-App (Child/Window Title):** Specific content within the parent app.
    - *Examples:* A specific YouTube video title, a website domain ("github.com"), or a document name.
    - *Code Ref:* `AppUsage.ProgramBreakdown` (Dictionary `<string, TimeSpan>`)
    - *Storage:* Stored as children of the main `AppUsage` object.

### Data Visualization Modes
- **Categories:** Aggregates usage by `AppTag` (Productivity, Social, etc.).
- **Apps:** Aggregates usage by `ProcessName`.
- **Sub-Apps:** Iterates through `ProgramBreakdown` to show granular usage (e.g., "VS Code - Project A" vs "VS Code - Project B").

### Sub-App Handling Logic
- **Truncation:** Sub-app names can be very long (URL titles). always truncate to ~30 chars for legends/charts.
- **Tagging:** Sub-apps can have their own tags separate from the parent app (e.g., `chrome.exe` = Untagged, but `youtube.com` = Entertainment).

---

## 4. 🛠️ Common Workarounds

### Self-Healing Logic
- The app includes "Orphan Tag" cleanup on startup. If tags disappear or get corrupted, the service or app startup logic usually re-binds or clears them.