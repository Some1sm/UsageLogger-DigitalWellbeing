using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalWellbeingService
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ActivityLogger _al = new ActivityLogger();
            Helpers.ServiceLogger.Log("Service", "Service started successfully.");

            while (true)
            {
                try
                {
                    _al.OnTimer();
                }
                catch (Exception ex)
                {
                    Helpers.ServiceLogger.LogError("OnTimer", ex);
                }
                Thread.Sleep(ActivityLogger.TIMER_INTERVAL_SEC * 1000);
            }
        }
    }
}
