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
    private static int _exitHandled = 0;

    public static void Shutdown()
    {
        if (Interlocked.Exchange(ref _exitHandled, 1) == 1) return;
        try
        {
            ServiceLogger.Log("Service", "Performing graceful shutdown and flushing buffers...");
            _activityLogger?.SaveOnExitAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            ServiceLogger.LogError("Shutdown", ex);
        }
        finally
        {
            TrayManager.Dispose();
        }
    }

    [STAThread]
    static void Main(string[] args)
    {
        // Single-instance check using Mutex
        const string mutexName = "Global\\UsageLoggerService_SingleInstance";
        bool createdNew;
        using var mutex = new Mutex(true, mutexName, out createdNew);
        
        if (!createdNew)
        {
            // Another instance is already running - exit silently
            ServiceLogger.Log("Service", "Another instance already running. Exiting.");
            return;
        }

        // Initialize tray icon
        TrayManager.Init();

        // Composition Root
        string logsPath = ApplicationPath.UsageLogsFolder;
        
        var sessionsRepo = new AppSessionRepository(logsPath);
        var usageRepo = new AppUsageRepository(logsPath);
        
        var sessionManager = new SessionManager(sessionsRepo);
        _activityLogger = new ActivityLogger(usageRepo, sessionManager);

        // Register Graceful Exit Handlers
        Application.ApplicationExit += (s, e) => Shutdown();
        AppDomain.CurrentDomain.ProcessExit += (s, e) => Shutdown();
        Microsoft.Win32.SystemEvents.SessionEnding += (s, e) => Shutdown();

        // Start async logger loop on a background task
        Task.Run(async () =>
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

        // Run Windows Forms message pump (keeps tray icon responsive)
        Application.Run();
    }
}

