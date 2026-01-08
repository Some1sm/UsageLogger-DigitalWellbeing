using DigitalWellbeing.Core;
using DigitalWellbeing.Core.Data;
using System;
using System.Threading.Tasks;

namespace DigitalWellbeingService;

class Program
{
    [STAThread]
    static async Task Main(string[] args)
    {
        // Composition Root
        string logsPath = ApplicationPath.UsageLogsFolder;
        
        var sessionsRepo = new AppSessionRepository(logsPath);
        var usageRepo = new AppUsageRepository(logsPath);
        
        var sessionManager = new SessionManager(sessionsRepo);
        var activityLogger = new ActivityLogger(usageRepo, sessionManager);

        Helpers.ServiceLogger.Log("Service", "Service started successfully (Async Mode).");

        // Main Loop
        while (true)
        {
            try
            {
                await activityLogger.OnTimerAsync();
            }
            catch (Exception ex)
            {
                Helpers.ServiceLogger.LogError("OnTimer", ex);
            }
            await Task.Delay(ActivityLogger.TIMER_INTERVAL_SEC * 1000);
        }
    }
}
