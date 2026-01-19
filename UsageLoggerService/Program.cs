#nullable enable
using UsageLogger.Core;
using UsageLogger.Core.Data;
using UsageLoggerService.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UsageLoggerService;

class Program
{
    private static ActivityLogger? _activityLogger;

    [STAThread]
    static void Main(string[] args)
    {
        // Initialize tray icon
        TrayManager.Init();

        // Composition Root
        string logsPath = ApplicationPath.UsageLogsFolder;
        
        var sessionsRepo = new AppSessionRepository(logsPath);
        var usageRepo = new AppUsageRepository(logsPath);
        
        var sessionManager = new SessionManager(sessionsRepo);
        _activityLogger = new ActivityLogger(usageRepo, sessionManager);

        // Start async logger loop on a background thread
        Thread loggerThread = new Thread(async () =>
        {
            // Async initialization
            await _activityLogger.InitializeAsync();
            ServiceLogger.Log("Service", "Service started successfully (Async Mode).");

            // Main Loop
            while (true)
            {
                try
                {
                    await _activityLogger.OnTimerAsync();
                }
                catch (Exception ex)
                {
                    ServiceLogger.LogError("OnTimer", ex);
                }
                await Task.Delay(ActivityLogger.TIMER_INTERVAL_SEC * 1000);
            }
        });
        loggerThread.IsBackground = true;
        loggerThread.Start();

        // Run Windows Forms message pump (keeps tray icon responsive)
        Application.Run();
    }
}

