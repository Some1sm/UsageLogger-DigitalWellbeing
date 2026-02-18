<h1 align="center">UsageLogger for Windows</h1>

<p align="center">
  <b>A premium app usage &amp; time tracker for Windows 11</b><br>
  Built with <b>WinUI 3</b> &amp; <b>Win2D</b> for a beautiful, native experience.
</p>

<p align="center">
  <a href="https://github.com/Some1sm/UsageLogger-DigitalWellbeing/releases"><img src="https://img.shields.io/badge/version-1.0.0-blue?style=flat-square" alt="Version"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPLv3-green?style=flat-square" alt="License"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2011-0078D4?style=flat-square&logo=windows11" alt="Platform">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 8">
</p>

---

## ✨ Features

### 📊 Dashboard & Analytics
- **Weekly Overview** — Interactive bar chart showing your usage trends over the past 7 days. Click any day to drill down.
- **Daily Breakdown** — Treemap visualization with hierarchical view by category, app, and sub-app.
- **Activity Heatmap** — Color-coded grid showing your active hours. Click any cell to jump to that day's timeline.
- **Smart Insights** — "Did you know?" style fun facts and trends about your digital habits.

### 🕒 Detailed Tracking
- **Sub-App Tracking** — Track individual window titles (browser tabs, documents, etc.) as children of their parent app.
- **Daily Timeline** — Visual session timeline with blocks for each app, including audio activity indicators.
- **Session History** — Browse detailed session logs for any date with duration and time ranges.
- **Incognito Mode** — Pause detailed window title tracking at any time for privacy.

### 🏷️ Organization
- **App Tagging** — Categorize apps into groups (Work, Social, Games, etc.) with custom colors derived from your Windows accent color.
- **Sub-App Tagging** — Assign different categories to specific window titles (e.g., "YouTube" as *Entertainment* vs "Docs" as *Productivity* within the same browser).
- **Custom Title Rules** — Define pattern-matching rules to automatically rename or group window titles.
- **Exclude Apps** — Filter out specific apps or system helper processes from tracking.

### ⏱️ Focus & Limits
- **Time Limits** — Set daily usage limits per app or sub-app.
- **Enforcement** — Receive notifications and optional blocking prompts (*Ignore*, *+5 min*, *Close App*) when limits are exceeded.
- **Focus Mode** — Strict and Chill modes with app whitelisting and scheduled focus sessions.
- **Force Refresh** — Instantly sync dashboard data from the background service buffer.

### 🌍 Localization
Full translation support for **11 languages**:
- 🇬🇧 English · 🇪🇸 Spanish · Catalan · 🇫🇷 French · 🇩🇪 German · 🇮🇹 Italian · 🇧🇷 Portuguese (BR) · 🇷🇺 Russian · 🇯🇵 Japanese · 🇰🇷 Korean · 🇨🇳 Chinese (Simplified)

### 🎨 Design
- **Native Windows 11 aesthetic** with Mica material and Acrylic surfaces.
- Full **Light / Dark theme** support following system settings.
- **Accent-color-aware** charts and tag colors.
- System tray integration with auto-start on login.

---

## 📥 Installation

### Installer (Recommended)
1. Download **`UsageLogger_Installer.exe`** from the [Releases](https://github.com/Some1sm/UsageLogger-DigitalWellbeing/releases) page.
2. Run the installer — Windows SmartScreen may appear since the app is unsigned; click **"More info" → "Run anyway"**.
3. The app and background service will be installed automatically.

### Portable
1. Download **`UsageLogger_Portable.zip`** from the [Releases](https://github.com/Some1sm/UsageLogger-DigitalWellbeing/releases) page.
2. Extract to any folder and run `UsageLogger.exe`.

### Requirements
- **Windows 10** (1903+) or **Windows 11**
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (x64)
- [Windows App SDK Runtime](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)

---

## 🛠️ Troubleshooting

### App crashes on launch
1. Uninstall the app.
2. Delete the contents of `%LOCALAPPDATA%\digital-wellbeing\`.
3. Reinstall the latest version.

### Service not tracking
- Ensure `UsageLoggerService.exe` is running (check Task Manager).
- Service debug logs are located at `%LOCALAPPDATA%\digital-wellbeing\service_debug.log`.

---

## 👨‍💻 For Developers

### Architecture
UsageLogger uses a **hybrid two-process architecture**:

| Component | Framework | Purpose |
|---|---|---|
| `UsageLogger` | .NET 8, WinUI 3 | UI client — visualization, settings, user interaction |
| `UsageLoggerService` | .NET 8 | Background service — silent process polling via Win32 API |
| `UsageLogger.Core` | .NET 8 | Shared library — models, repositories, utilities |
| `UsageLogger.Setup` | .NET Framework 4.7.2 | Self-extracting installer/uninstaller |

### Key Technologies
- **Win2D** (DirectX) for hardware-accelerated custom chart rendering
- **WinUI3Localizer** for runtime language switching
- **NAudio** for audio activity detection
- **H.NotifyIcon** for system tray integration

### Building from Source
```powershell
# Clone the repository
git clone https://github.com/Some1sm/UsageLogger-DigitalWellbeing.git
cd UsageLogger-DigitalWellbeing

# Build the installer (~30 MB)
.\publish.ps1
```

The script will:
1. Build all projects in `Release` configuration.
2. Package the app and service into a portable zip.
3. Build the self-extracting installer.
4. Output `UsageLogger_Installer.exe` and `UsageLogger_Portable.zip` in the root directory.

### Data Storage
- **User data**: `%LOCALAPPDATA%\digital-wellbeing\`
- **Configuration**: `user_preferences.json` (tags, limits, rules, settings)
- **Session logs**: `sessions_YYYY-MM-DD.log` (daily usage data)

---

## 📄 License

This project is licensed under the **GNU General Public License v3.0** — see the [LICENSE](LICENSE) file for details.

---

<p align="center">
  <sub>Created by <b>David Cepero</b> · © 2025-2026</sub>
</p>
