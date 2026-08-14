#nullable enable
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UsageLogger.Core.Models;

namespace UsageLogger.Core.Helpers;

public static class PowerTracker
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;         // 0 = Offline, 1 = Online, 255 = Unknown
        public byte BatteryFlag;          // 1 = High, 2 = Low, 4 = Critical, 8 = Charging, 128 = No battery, 255 = Unknown
        public byte BatteryLifePercent;   // 0-100, or 255 if unknown
        public byte SystemStatusFlag;     // 1 = Battery saver active
        public int BatteryLifeTime;       // Seconds remaining, or -1
        public int BatteryFullLifeTime;   // Full lifetime in seconds, or -1
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;

        public ulong ToUInt64() => ((ulong)dwHighDateTime << 32) | dwLowDateTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    // CPU measurement state
    private static ulong _lastIdleTime = 0;
    private static ulong _lastKernelTime = 0;
    private static ulong _lastUserTime = 0;
    private static DateTime _lastCpuSampleTime = DateTime.MinValue;
    private static double _cachedCpuUsage = 5.0;

    // Default TDP estimate based on core count
    private static readonly double BaseTdpWatts = Math.Clamp(Environment.ProcessorCount * 6.0, 25.0, 95.0);

    /// <summary>
    /// Gets the current system CPU load percentage (0.0 to 100.0%).
    /// </summary>
    public static double GetSystemCpuUsage()
    {
        DateTime now = DateTime.UtcNow;
        if ((now - _lastCpuSampleTime).TotalMilliseconds < 800)
        {
            return _cachedCpuUsage;
        }

        if (GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime))
        {
            ulong currentIdle = idleTime.ToUInt64();
            ulong currentKernel = kernelTime.ToUInt64();
            ulong currentUser = userTime.ToUInt64();

            if (_lastKernelTime != 0 && _lastUserTime != 0)
            {
                ulong idleDiff = currentIdle - _lastIdleTime;
                ulong kernelDiff = currentKernel - _lastKernelTime;
                ulong userDiff = currentUser - _lastUserTime;
                ulong totalDiff = kernelDiff + userDiff;

                if (totalDiff > 0)
                {
                    double cpu = 100.0 * (1.0 - ((double)idleDiff / totalDiff));
                    _cachedCpuUsage = Math.Clamp(cpu, 0.0, 100.0);
                }
            }

            _lastIdleTime = currentIdle;
            _lastKernelTime = currentKernel;
            _lastUserTime = currentUser;
            _lastCpuSampleTime = now;
        }

        return _cachedCpuUsage;
    }

    /// <summary>
    /// Retrieves a live snapshot of the device power draw and battery status.
    /// </summary>
    public static PowerSnapshot GetPowerSnapshot()
    {
        var snapshot = new PowerSnapshot();
        double cpuUsage = GetSystemCpuUsage();

        if (GetSystemPowerStatus(out SYSTEM_POWER_STATUS status))
        {
            bool hasBattery = (status.BatteryFlag & 128) == 0 && status.BatteryFlag != 255;
            bool isAc = status.ACLineStatus == 1;
            bool isCharging = (status.BatteryFlag & 8) != 0;

            snapshot.IsBatteryPresent = hasBattery;
            snapshot.IsCharging = isCharging;
            snapshot.BatteryPercentage = status.BatteryLifePercent <= 100 ? status.BatteryLifePercent : -1;

            if (hasBattery && !isAc)
            {
                // Discharging on battery
                snapshot.PowerSource = PowerSourceType.Battery;
                
                if (status.BatteryLifeTime > 0 && status.BatteryLifeTime < 86400 * 2)
                {
                    snapshot.EstimatedTimeRemaining = TimeSpan.FromSeconds(status.BatteryLifeTime);
                    // Approximate typical battery capacity ~52 Wh if not specified
                    double batteryCapWh = 52.0;
                    double hoursLeft = snapshot.EstimatedTimeRemaining.Value.TotalHours;
                    if (hoursLeft > 0.05 && snapshot.BatteryPercentage > 0)
                    {
                        double remainingWh = batteryCapWh * (snapshot.BatteryPercentage / 100.0);
                        snapshot.InstantDrawWatts = Math.Clamp(remainingWh / hoursLeft, 4.0, 85.0);
                        snapshot.IsSimulatedDraw = false;
                    }
                }

                if (snapshot.InstantDrawWatts <= 0)
                {
                    // Fallback to laptop battery discharge model
                    double baseLaptopIdle = 6.5;
                    double dynamicCpu = (cpuUsage / 100.0) * (BaseTdpWatts * 0.45);
                    snapshot.InstantDrawWatts = Math.Round(baseLaptopIdle + dynamicCpu, 1);
                    snapshot.IsSimulatedDraw = true;
                }
            }
            else
            {
                // On AC power or Desktop PC
                snapshot.PowerSource = isAc ? PowerSourceType.AC : PowerSourceType.Unknown;
                
                double baseIdle = hasBattery ? 8.0 : 25.0; // Desktop idle ~25W, laptop on AC ~8W
                double dynamicCpu = (cpuUsage / 100.0) * BaseTdpWatts;
                snapshot.InstantDrawWatts = Math.Round(baseIdle + dynamicCpu, 1);
                snapshot.IsSimulatedDraw = true;
            }

            // Build status and tooltip text
            if (snapshot.PowerSource == PowerSourceType.Battery)
            {
                string timeStr = snapshot.EstimatedTimeRemaining.HasValue
                    ? StringHelper.FormatDurationCompact(snapshot.EstimatedTimeRemaining.Value) + " left"
                    : "Discharging";
                snapshot.PowerStatusText = $"🔋 {snapshot.BatteryPercentage}% • {snapshot.InstantDrawWatts:F1} W";
                snapshot.PowerDetailTooltip = $"Battery: {snapshot.BatteryPercentage}%\nRate: {snapshot.InstantDrawWatts:F1} W discharge\nStatus: {timeStr}";
            }
            else if (snapshot.IsBatteryPresent)
            {
                string state = isCharging ? $"Charging ({snapshot.BatteryPercentage}%)" : $"Plugged in ({snapshot.BatteryPercentage}%)";
                snapshot.PowerStatusText = $"⚡ {snapshot.InstantDrawWatts:F1} W (AC)";
                snapshot.PowerDetailTooltip = $"{state}\nActive Draw: {snapshot.InstantDrawWatts:F1} W (Estimated)\nCPU Load: {cpuUsage:F0}%";
            }
            else
            {
                // Pure Desktop
                snapshot.PowerStatusText = $"⚡ {snapshot.InstantDrawWatts:F1} W";
                snapshot.PowerDetailTooltip = $"Desktop AC Power\nActive Draw: {snapshot.InstantDrawWatts:F1} W (Estimated)\nCPU Load: {cpuUsage:F0}%";
            }
        }
        else
        {
            // Fallback estimation
            double fallbackWatts = Math.Round(20.0 + (cpuUsage / 100.0) * BaseTdpWatts, 1);
            snapshot.InstantDrawWatts = fallbackWatts;
            snapshot.IsSimulatedDraw = true;
            snapshot.PowerStatusText = $"⚡ {fallbackWatts:F1} W";
            snapshot.PowerDetailTooltip = $"Estimated Draw: {fallbackWatts:F1} W\nCPU Load: {cpuUsage:F0}%";
        }

        return snapshot;
    }

    /// <summary>
    /// Computes the estimated power impact and Watts for a specific process given its activity.
    /// </summary>
    public static (double ProcessWatts, PowerImpactLevel Level) EstimateProcessPower(bool isForeground, bool hasAudio, double durationSeconds, double systemWatts)
    {
        double procWatts = 0.5; // Base passive background

        if (isForeground)
        {
            procWatts += Math.Max(2.5, systemWatts * 0.45);
        }

        if (hasAudio)
        {
            procWatts += 1.8;
        }

        PowerImpactLevel level = procWatts switch
        {
            < 2.0 => PowerImpactLevel.VeryLow,
            < 6.0 => PowerImpactLevel.Low,
            < 14.0 => PowerImpactLevel.Moderate,
            < 28.0 => PowerImpactLevel.High,
            _ => PowerImpactLevel.VeryHigh
        };

        return (procWatts, level);
    }

    /// <summary>
    /// Calculates energy in Watt-hours (Wh) for a given power in Watts over a duration.
    /// </summary>
    public static double CalculateEnergyWattHours(double watts, TimeSpan duration)
    {
        return (watts * duration.TotalSeconds) / 3600.0;
    }
}
