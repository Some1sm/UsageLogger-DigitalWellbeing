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
4.  **Logic Checks**: NEVER rely on `MenuFlyoutItem.Text` or UI strings for logic (e.g. `if (item.Text == "Category")`). Use `Tag` or `Name` properties instead, as Text changes with locale.

## 5. UI & Charting Guidelines
*   **Aesthetics**: Mimic native Windows 11 design. Use Mica/Acrylic where possible.
*   **LiveCharts2 / SkiaSharp**:
    *   **Text Rendering**: `SKPaint.Typeface` and `SKPaint.TextSize` are **DEPRECATED**. Use the modern **`SKFont(typeface, size)`** API for all text operations.
    *   **Tooltips**: Use `YToolTipLabelFormatter` (Cartesian) or `ToolTipLabelFormatter` (Pie/Heatmap).
    *   **Format**: Always format durations as `Xh Ym` (never decimals like "1.5h").
    *   **Colors**: Listen to `UISettings.ColorValuesChanged` to generate dynamic gradients based on the user's System Accent Color.
*   **Timeline Visualization**:
    *   **Clutter Control**: Use label collision detection (hide overlapping text) and enforces minimum height thresholds (16px text, 28px sub-details).
    *   **Smart Tooltips**: Prioritize the smallest/most specific non-AFK block under the cursor to handle overlapping elements (e.g. Chrome vs Background).
    *   **Incognito Merging**: When `IncognitoMode` is true, force app grouping by `ProcessName` (ignoring Window Titles) to merge fragmented sessions into single continuous blocks.
*   **Interactive Spin/Physics**:
        *   **Animation Conflict**: LiveCharts internal animations (`AnimationsSpeed`) conflict with high-frequency manual updates. **Solution**: Use Hybrid Animation Strategy (TimeSpan.Zero during drag/spin, Restore defaults when idle).
        *   **Input Handling**: Standard [PointerPressed](cci:1://file:///h:/Coding/DigitalWellbeing/DigitalWellbeing_myworkGemini2/DigitalWellbeingWinUI3/Views/DayAppUsagePage.xaml.cs:805:8-836:9) on Chart controls is flaky (misses gaps between slices). **Solution**: Always attach input handlers to the **Container** (Grid/Border), not the Chart control itself.
        *   **UX / Tooltips**: Hide tooltips during drag interactions (`TooltipPosition.Hidden`) to prevent them from blocking clicks or creating visual noise ("Passthrough" effect).
        *   **Math**: Use **Absolute Angle Tracking** (StartAngle + Delta) instead of relative accumulation to prevent drift/jitter.


## 6. Coding Standards
*   **Service Robustness**:
    *   **Global Exception Handling**: The Background Service loop (`Program.cs`) MUST be wrapped in a global `try-catch` to log errors (to file) and prevent crashes.
    *   **Window Detection Fallback**: `GetForegroundWindow` can fail (return 0) on Desktop or Transitions. Always implement fallbacks: `GetForegroundWindow` -> `WindowFromPoint(Cursor)` -> `GetGUIThreadInfo`.
    *   **System Calls**: `Process.GetProcessById` can fail. Always wrap in try-catch and log failures rather than crashing or silently returning.
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
*   **Service Debug Logs**: `%LocalAppData%\digital-wellbeing\service_debug.log`.
*   **Installer Output**: Root workspace folder.

## 8. Data Persistence Strategy
The application supports two modes for saving usage data, controlled by `UserPreferences.UseRamCache`:
1.  **RAM Cache (Default)**:
    *   **Behavior**: Usage data is buffered in RAM and flushed to disk every 5 minutes (or `DataFlushIntervalSeconds`).
    *   **Pros**: Reduces disk I/O, extends SSD life, better performance.
    *   **Cons**: Potential data loss of up to 5 minutes if the Service crashes (unlikely).
2.  **Direct Write**:
    *   **Behavior**: Flushes data to disk every **1 second** (logic in `ActivityLogger.cs` overrides interval).
    *   **Pros**: Maximum data safety (crash recovery).
    *   **Cons**: High disk I/O frequency.
*   **Implementation Details**:
    *   `ActivityLogger` reads `user_preferences.json` every 30s.
    *   If `UseRamCache` is false, it forces `_bufferFlushIntervalSec = 1`.

## 9. 🌍 Localization Maintenance (Best Practices)
**Root Cause of Translation Failures**: WinUI3Localizer **cannot** override hardcoded fallback values in XAML attributes. If you set `Text="Hello"` alongside `l:Uids.Uid="MyKey"`, the hardcoded `"Hello"` will always display.

### ✅ DO (Correct Patterns)
*   **XAML Elements**: Only set `l:Uids.Uid`, never add fallback `Text=`, `Content=`, `Header=`, `PlaceholderText=`, `Title=`, `OnContent=`, `OffContent=`, `PrimaryButtonText=`, `CloseButtonText=`, etc.
    ```xml
    <!-- CORRECT -->
    <TextBlock l:Uids.Uid="MyLabel"/>
    <Button l:Uids.Uid="MyBtn"/>
    <ContentDialog l:Uids.Uid="MyDialog"/>
    <ToggleSwitch l:Uids.Uid="MyToggle"/>
    ```
*   **Resource Keys**: Use `.Text`, `.Content`, `.Header`, `.PlaceholderText`, `.Title`, `.OnContent`, `.OffContent`, `.PrimaryButtonText`, `.CloseButtonText` suffixes.
    ```xml
    <!-- In Resources.resw -->
    <data name="MyLabel.Text"><value>My Label</value></data>
    <data name="MyToggle.OnContent"><value>On</value></data>
    <data name="MyDialog.Title"><value>My Dialog</value></data>
    ```
*   **Code-Behind Dialogs**: Use `LocalizationHelper.GetString("Key")` for strings in `ContentDialog` created in C#.
    ```csharp
    var dialog = new ContentDialog
    {
        Title = string.Format(LocalizationHelper.GetString("Dialog_Title"), appName),
        PrimaryButtonText = LocalizationHelper.GetString("Dialog_Save"),
        SecondaryButtonText = LocalizationHelper.GetString("Dialog_Cancel")
    };
    ```
*   **New Translations**: Always add keys to `Strings/en-US/Resources.resw` (base) AND all other language folders (e.g., `ca-ES`, `es-ES`).

### ❌ DON'T (Anti-Patterns)
*   **Never** add hardcoded fallback text alongside `l:Uids.Uid`:
    ```xml
    <!-- WRONG - "Settings" will always show, ignoring localizer -->
    <TextBlock l:Uids.Uid="Settings_Title" Text="Settings"/>
    ```
*   **Never** forget to remove fallback values when adding localization to existing elements.
*   **Never** create dialogs in code-behind with hardcoded strings like `Title = "Edit Rule"`.

### 🔧 Debugging Translations
1.  If a UI element shows English despite the app being in another language, check the XAML for hardcoded fallback values.
2.  Verify the key exists in the target language's `Resources.resw` with the correct suffix (e.g., `.Text`, `.Content`).
3.  Compare with a working localized page (e.g., `SettingsPage.xaml`) to identify pattern differences.