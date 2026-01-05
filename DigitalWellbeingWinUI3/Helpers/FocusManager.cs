using DigitalWellbeing.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace DigitalWellbeingWinUI3.Helpers
{
    /// <summary>
    /// Manages Focus Sessions: persistence, monitoring, and enforcement.
    /// </summary>
    public class FocusManager
    {
        #region Singleton
        private static FocusManager _instance;
        private static readonly object _lock = new object();
        
        public static FocusManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new FocusManager();
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region P/Invoke
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
        #endregion

        #region Whitelisted Processes
        // These processes are NEVER killed in Focus Mode
        private static readonly HashSet<string> _whitelistedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "explorer",
            "taskmgr",
            "dwm",
            "csrss",
            "winlogon",
            "services",
            "lsass",
            "svchost",
            "ApplicationFrameHost",
            "ShellExperienceHost",
            "StartMenuExperienceHost",
            "SearchHost",
            "DigitalWellbeingWinUI3",
            "DigitalWellbeing"
        };
        #endregion

        #region State
        private List<FocusSession> _sessions = new List<FocusSession>();
        private DispatcherTimer _monitorTimer;
        private DateTime _lastNotificationTime = DateTime.MinValue;
        private string _lastNotifiedProcessName = null;
        private bool _isRunning = false;

        public XamlRoot XamlRoot { get; set; }
        public List<FocusSession> Sessions => _sessions;
        public bool IsMonitoring => _isRunning;
        
        // Currently active session (if any)
        public FocusSession ActiveSession { get; private set; }
        #endregion

        #region File Path
        private static readonly string _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "digital-wellbeing",
            "focus_schedule.json");
        #endregion

        private FocusManager()
        {
            Load();
            InitializeMonitor();
        }

        #region Persistence
        public void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    _sessions = JsonSerializer.Deserialize(json, DigitalWellbeing.Core.Contexts.AppJsonContext.Default.ListFocusSession) ?? new List<FocusSession>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FocusManager] Load Error: {ex.Message}");
                _sessions = new List<FocusSession>();
            }
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(_filePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(_sessions, DigitalWellbeing.Core.Contexts.AppJsonContext.Default.ListFocusSession);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FocusManager] Save Error: {ex.Message}");
            }
        }
        #endregion

        #region CRUD
        public void AddSession(FocusSession session)
        {
            _sessions.Add(session);
            Save();
        }

        public void UpdateSession(FocusSession session)
        {
            var existing = _sessions.FirstOrDefault(s => s.Id == session.Id);
            if (existing != null)
            {
                int index = _sessions.IndexOf(existing);
                _sessions[index] = session;
                Save();
            }
        }

        public void RemoveSession(string sessionId)
        {
            _sessions.RemoveAll(s => s.Id == sessionId);
            Save();
        }
        #endregion

        #region Monitor
        private void InitializeMonitor()
        {
            _monitorTimer = new DispatcherTimer();
            _monitorTimer.Interval = TimeSpan.FromSeconds(2);
            _monitorTimer.Tick += MonitorTick;
        }

        public void StartMonitoring()
        {
            if (!_isRunning)
            {
                _isRunning = true;
                _monitorTimer.Start();
                Debug.WriteLine("[FocusManager] Monitoring Started");
            }
        }

        public void StopMonitoring()
        {
            if (_isRunning)
            {
                _isRunning = false;
                _monitorTimer.Stop();
                ActiveSession = null;
                Debug.WriteLine("[FocusManager] Monitoring Stopped");
            }
        }

        private void MonitorTick(object sender, object e)
        {
            try
            {
                // Find any active session
                var activeSession = _sessions.FirstOrDefault(s => s.IsEnabled && s.IsActiveNow());
                ActiveSession = activeSession;

                if (activeSession == null)
                {
                    return; // No active session, nothing to enforce
                }

                // Get foreground window info
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return;

                GetWindowThreadProcessId(hwnd, out uint processId);
                if (processId == 0) return;

                Process proc = null;
                try
                {
                    proc = Process.GetProcessById((int)processId);
                }
                catch { return; }

                string currentProcessName = proc.ProcessName;

                // Get window title for sub-app matching
                var sb = new System.Text.StringBuilder(256);
                GetWindowText(hwnd, sb, 256);
                string windowTitle = sb.ToString();

                proc.Dispose();

                // Check if current app matches target
                bool isMatch = IsAppMatch(activeSession, currentProcessName, windowTitle);

                if (!isMatch)
                {
                    // Check if it's a whitelisted process
                    if (_whitelistedProcesses.Contains(currentProcessName))
                    {
                        return; // Always allow whitelisted
                    }

                    // Enforce based on mode
                    EnforceSession(activeSession, currentProcessName, processId);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FocusManager] Monitor Error: {ex.Message}");
            }
        }

        private bool IsAppMatch(FocusSession session, string processName, string windowTitle)
        {
            // Check process name match (case-insensitive)
            if (!string.Equals(session.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // If no specific sub-app is set, any window of this process is fine
            if (string.IsNullOrEmpty(session.ProgramName))
            {
                return true;
            }

            // Check if window title contains the target sub-app name
            return windowTitle.IndexOf(session.ProgramName, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void EnforceSession(FocusSession session, string violatingProcess, uint processId)
        {
            switch (session.Mode)
            {
                case FocusMode.Chill:
                    ShowChillNotification(session, violatingProcess);
                    break;

                case FocusMode.Normal:
                    ShowNormalDialog(session, violatingProcess);
                    break;

                case FocusMode.Focus:
                    KillProcess(processId, violatingProcess);
                    break;
            }
        }
        #endregion

        #region Enforcement Actions
        private void ShowChillNotification(FocusSession session, string violatingProcess)
        {
            // Throttle notifications to once per minute per process
            if ((DateTime.Now - _lastNotificationTime).TotalSeconds < 60 && 
                _lastNotifiedProcessName == violatingProcess)
            {
                return;
            }

            _lastNotificationTime = DateTime.Now;
            _lastNotifiedProcessName = violatingProcess;

            try
            {
                var builder = new AppNotificationBuilder()
                    .AddText($"🎯 Focus Session Active: {session.Name}")
                    .AddText($"You're using '{violatingProcess}' instead of '{session.ProcessName}'");

                var notification = builder.BuildNotification();
                AppNotificationManager.Default.Show(notification);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FocusManager] Toast Error: {ex.Message}");
            }
        }

        private async void ShowNormalDialog(FocusSession session, string violatingProcess)
        {
            // Throttle dialogs to once per 10 seconds to avoid spam
            if ((DateTime.Now - _lastNotificationTime).TotalSeconds < 10)
            {
                return;
            }

            _lastNotificationTime = DateTime.Now;

            if (XamlRoot == null) return;

            try
            {
                var dialog = new ContentDialog
                {
                    Title = "🎯 Focus Session Active",
                    Content = $"You're supposed to be using '{session.ProcessName}' right now!\n\n" +
                              $"Session: {session.Name}\n" +
                              $"Ends at: {session.EndTime:hh\\:mm}",
                    PrimaryButtonText = "OK",
                    CloseButtonText = "Disable Session",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.None) // Close button
                {
                    session.IsEnabled = false;
                    Save();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FocusManager] Dialog Error: {ex.Message}");
            }
        }

        private void KillProcess(uint processId, string processName)
        {
            // Double-check whitelist
            if (_whitelistedProcesses.Contains(processName))
            {
                Debug.WriteLine($"[FocusManager] Skipping kill for whitelisted: {processName}");
                return;
            }

            try
            {
                var proc = Process.GetProcessById((int)processId);
                proc.Kill();
                Debug.WriteLine($"[FocusManager] Killed process: {processName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FocusManager] Kill Error: {ex.Message}");
            }
        }
        #endregion

        #region App Cache
        private static readonly string _appCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "digital-wellbeing",
            "known_apps.json");

        private HashSet<string> _knownAppsCache = null;

        /// <summary>
        /// Gets a list of all unique process names. Uses cache first, falls back to 30-day scan.
        /// </summary>
        public async Task<List<string>> GetHistoricalAppNamesAsync()
        {
            return await Task.Run(() =>
            {
                // Try to load from cache first
                if (_knownAppsCache == null)
                {
                    _knownAppsCache = LoadAppCache();
                }

                // If cache is empty or doesn't exist, do a full scan
                if (_knownAppsCache.Count == 0)
                {
                    Debug.WriteLine("[FocusManager] Cache empty, scanning last 30 days...");
                    ScanAndCacheApps(30);
                }

                return _knownAppsCache.OrderBy(a => a).ToList();
            });
        }

        /// <summary>
        /// Loads the known apps cache from disk.
        /// </summary>
        private HashSet<string> LoadAppCache()
        {
            try
            {
                if (File.Exists(_appCachePath))
                {
                    string json = File.ReadAllText(_appCachePath);
                    var list = JsonSerializer.Deserialize(json, DigitalWellbeing.Core.Contexts.AppJsonContext.Default.ListString);
                    if (list != null)
                    {
                        Debug.WriteLine($"[FocusManager] Loaded {list.Count} apps from cache");
                        return new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FocusManager] Cache load error: {ex.Message}");
            }
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Saves the known apps cache to disk.
        /// </summary>
        private void SaveAppCache()
        {
            try
            {
                string dir = Path.GetDirectoryName(_appCachePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var list = _knownAppsCache.OrderBy(a => a).ToList();
                string json = JsonSerializer.Serialize(list, DigitalWellbeing.Core.Contexts.AppJsonContext.Default.ListString);
                File.WriteAllText(_appCachePath, json);
                Debug.WriteLine($"[FocusManager] Saved {list.Count} apps to cache");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FocusManager] Cache save error: {ex.Message}");
            }
        }

        /// <summary>
        /// Scans the last N days of logs and updates the cache.
        /// </summary>
        private void ScanAndCacheApps(int days)
        {
            try
            {
                string logsFolder = DigitalWellbeing.Core.ApplicationPath.UsageLogsFolder;
                var repo = new DigitalWellbeing.Core.Data.AppSessionRepository(logsFolder);

                for (int i = 0; i < days; i++)
                {
                    var sessions = repo.GetSessionsForDate(DateTime.Now.AddDays(-i));
                    foreach (var s in sessions)
                    {
                        if (!string.IsNullOrEmpty(s.ProcessName))
                            _knownAppsCache.Add(s.ProcessName);
                    }
                }

                // Save to disk for future use
                SaveAppCache();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FocusManager] Scan error: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds a new app to the cache. Call this when new apps are encountered.
        /// </summary>
        public void RegisterApp(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return;

            if (_knownAppsCache == null)
                _knownAppsCache = LoadAppCache();

            if (_knownAppsCache.Add(processName))
            {
                // New app added, save cache
                SaveAppCache();
            }
        }
        #endregion
    }
}

