# Digital Wellbeing for Windows v0.8.1 (Alpha Preview) 🚀

**This update brings a major visual overhaul to the Distribution Analysis and critical bug fixes across the entire application.**

## ✨ What's New

### 🗺️ New Treemap Visualization
We've completely replaced the old Pie Charts with a modern **Treemap** for analyzing your app usage.
- **Hierarchical View**: Visualize your usage by Categories, Apps, and Sub-Apps (window titles) in a single compact view.
- **Auto-Contrast**: Text labels intelligently switch between black and white to ensure readability on any background color.
- **System Integration**: The chart now uses a **monochromatic palette** derived from your Windows accent color.
- **Maximize Space**: The layout algorithm has been rewritten to use 100% of the available screen real estate.

### 🖱️ Interactvity & Management
- **Context Menus Restored**: Right-click any app in the list to set **Time Limits**, apply **Tags**, or **Exclude** it from tracking.
- **Heatmap Navigation**: You can now click on any cell in the "Activity Map" to jump directly to the detailed timeline for that specific day.
- **Tray Icon**: Fixed an issue where the system tray menu items (Open, Settings, Exit) were unresponsive.

### ⚙️ Settings & UI Improvements
- **"Apply Changes" Banner**: Now appears smoothly as an overlay without shifting the entire page down.
- **Visual Improvements**: Fixed Settings page background color and improved padding on the Daily Timeline view.
- **Instant Feedback**: Typing in number fields now immediately triggers the "Apply" prompt.

### 🐛 Bug Fixes & Refinements
- **Historical Data**: Fixed rendering for legacy log files, ensuring your past history (pre-Dec 2025) is visible.
- **Sub-App Tagging**: Fixed a bug where tagging a specific window title (e.g., a specific website) would incorrectly tag the entire browser.
- **Foreign Characters**: Fixed an issue where window titles with emojis or non-English characters weren't displaying correctly (UTF-8 encoding fix).
- **Time Formatting**: Standardized time displays to the cleaner `1h 27m 20s` format.
- **Build System**: Resolved build errors preventing the creation of a working installer.

---
### 📥 Installation 
1. Download `DigitalWellbeing_Installer.exe`
2. Run the installer (SmartScreen may appear as this is a new unsigned app - click "Run Anyway")
3. Project is open source! Feel free to contribute or report issues.

---
*Created by David Cepero. Copyright © 2025.*
