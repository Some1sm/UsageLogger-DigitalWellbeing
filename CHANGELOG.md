# Changelog

All notable changes to **UsageLogger for Windows** are documented in this file.

---

## [1.0.0] — 2026-02-16

### 🚀 Highlights
The **1.0.0 stable release** represents a complete rewrite from the original WPF prototype into a modern WinUI 3 application with hardware-accelerated Win2D charting, full internationalization, and a robust two-process architecture.

### ✨ Added
- **Win2D Rendering Engine** — Replaced LiveCharts2/SkiaSharp with custom Win2D (DirectX) charts for hardware-accelerated, buttery-smooth rendering.
- **Treemap Visualization** — Replaced pie charts with a hierarchical treemap showing usage by Category → App → Sub-App with auto-contrast text labels.
- **Activity Heatmap** — Added a color-coded weekly heatmap showing active hours, with click-to-navigate to any day's timeline.
- **Daily Timeline** — Visual session timeline with colored blocks per app, including audio activity indicators and smart tooltip collision detection.
- **Force Refresh** — Dashboard button to instantly sync data from the background service buffer, bypassing the 5-minute flush interval.
- **Smart Insights** — "Did you know?" style data-driven fun facts and trends on the dashboard.
- **Focus Mode** — Strict and Chill modes with app whitelisting and scheduled focus sessions, enforced independently by the background service.
- **Time Limit Enforcement** — Notification popups with *Ignore*, *+5 min*, and *Close App* actions when usage limits are exceeded, enforced by the service process.
- **Sub-App Tagging** — Assign different category tags to specific window titles within the same parent application.
- **Custom Title Rules** — Pattern-matching rules to rename or group window titles (contains, starts with, regex).
- **Localization (11 languages)** — Full UI translation: English, Spanish, Catalan, French, German, Italian, Portuguese (BR), Russian, Japanese, Korean, Chinese (Simplified).
- **Report Bug button** — In-app link to submit issues directly from the Settings page.
- **GNU GPLv3 License** — Project relicensed under the GNU General Public License v3.0.
- **Portable distribution** — Added `UsageLogger_Portable.zip` alongside the installer for USB/no-install usage.

### 🔧 Changed
- **Project Rename** — Renamed from "Digital Wellbeing for Windows" to "UsageLogger for Windows" across all binaries and branding.
- **Service Architecture** — Migrated the background service from .NET Framework 4.7.2 to **.NET 8**, with global exception handling and multi-tier window detection fallback (`GetForegroundWindow` → `WindowFromPoint` → `GetGUIThreadInfo`).
- **Audio Tracking** — Migrated from CSCore to **NAudio** with split audio detection blocks and retry mechanisms.
- **Unified Configuration** — Consolidated scattered config files (`app_tags.json`, `title_tags.json`, etc.) into a single `user_preferences.json` with automatic migration on first launch.
- **RAM Cache (Default)** — Usage data is buffered in RAM and flushed to disk every 5 minutes to reduce SSD wear, with a direct-write option for maximum data safety.
- **Tray Icon** — Consolidated from dual tray icons (UI + Service) into a single Service-managed system tray icon.
- **Settings Page** — Refactored code-behind into dedicated helper classes (`TagSettingsHelper`, `LogLocationHelper`, `SubAppRulesHelper`) for maintainability.
- **Installer** — Optimized to ~30 MB with cleaned framework language folders and embedded uninstaller.

### 🐛 Fixed
- Fixed service randomly stopping due to unhandled exceptions (added global try-catch in service loop).
- Fixed sub-app tagging applying to the parent app instead of the selected sub-app.
- Fixed heatmap click navigation to correctly jump to the target day.
- Fixed historical data not rendering for dates before the session-log format migration.
- Fixed settings page background color inconsistency.
- Fixed "Apply Changes" banner shifting the settings page content.
- Fixed system tray menu items (Open, Settings, Exit) being unresponsive.
- Fixed window titles with emojis/non-English characters not displaying correctly (UTF-8).
- Fixed bar chart disappearing when the app window is resized to very small dimensions.
- Fixed timeline blocks showing generic parent app name instead of specific sub-app name.
- Fixed chart generation crash (`KeyNotFoundException` in tag aggregation).
- Fixed localization resource loading failures for non-English locales.

---

*For previous release notes, see the [Releases](https://github.com/Some1sm/UsageLogger-DigitalWellbeing/releases) page.*

*Created by David Cepero · © 2025-2026*
