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

    // Configured average system power baseline (from user settings, default 150W)
    public static double ConfiguredAvgWatts { get; set; } = 150.0;

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
                    // Approximate typical battery capacity ~56 Wh if not specified
                    double batteryCapWh = 56.0;
                    double hoursLeft = snapshot.EstimatedTimeRemaining.Value.TotalHours;
                    if (hoursLeft > 0.05 && snapshot.BatteryPercentage > 0)
                    {
                        double remainingWh = batteryCapWh * (snapshot.BatteryPercentage / 100.0);
                        snapshot.InstantDrawWatts = Math.Clamp(remainingWh / hoursLeft, 5.0, 95.0);
                        snapshot.IsSimulatedDraw = false;
                    }
                }

                if (snapshot.InstantDrawWatts <= 0)
                {
                    // Fallback to laptop battery discharge model
                    double baseLaptopIdle = 8.5;
                    double dynamicCpu = (cpuUsage / 100.0) * 35.0;
                    snapshot.InstantDrawWatts = Math.Round(baseLaptopIdle + dynamicCpu, 1);
                    snapshot.IsSimulatedDraw = true;
                }

                string timeStr = snapshot.EstimatedTimeRemaining.HasValue
                    ? StringHelper.FormatDurationCompact(snapshot.EstimatedTimeRemaining.Value) + " left"
                    : "Discharging";
                snapshot.PowerStatusText = $"🔋 {snapshot.BatteryPercentage}% • {snapshot.InstantDrawWatts:F1} W";
                snapshot.PowerDetailTooltip = $"Battery: {snapshot.BatteryPercentage}%\nRate: {snapshot.InstantDrawWatts:F1} W discharge\nStatus: {timeStr}";
            }
            else if (hasBattery && isAc)
            {
                // Laptop on AC Power
                snapshot.PowerSource = PowerSourceType.AC;
                double laptopPlatform = 10.0;
                double laptopCpu = 15.0 + (cpuUsage / 100.0) * 35.0;
                double laptopGpu = 8.0;
                double totalLaptopWatts = Math.Round(laptopPlatform + laptopCpu + laptopGpu, 1);

                snapshot.InstantDrawWatts = totalLaptopWatts;
                snapshot.IsSimulatedDraw = true;

                string state = isCharging ? $"Charging ({snapshot.BatteryPercentage}%)" : $"Plugged in ({snapshot.BatteryPercentage}%)";
                snapshot.PowerStatusText = $"⚡ {snapshot.InstantDrawWatts:F1} W (AC)";
                snapshot.PowerDetailTooltip = $"{state}\nEstimated System Draw: {snapshot.InstantDrawWatts:F1} W\n├─ CPU: {laptopCpu:F0} W ({cpuUsage:F0}% load)\n├─ GPU/Display: {laptopGpu:F0} W\n└─ Platform: {laptopPlatform:F0} W";
            }
            else
            {
                // Desktop PC (No battery - full desktop hardware platform)
                snapshot.PowerSource = PowerSourceType.AC;
                
                double baseline = ConfiguredAvgWatts > 20.0 ? ConfiguredAvgWatts : 150.0;
                
                // Typical modern desktop split: 30% Platform, 50% CPU Package, 20% GPU (2D/Display)
                double platformBase = Math.Round(baseline * 0.30, 1); // ~45W for 150W baseline
                double cpuBase = Math.Round(baseline * 0.50 * 0.60, 1); // ~45W package idle
                double cpuPeak = Math.Round(baseline * 0.50 * 1.50, 1); // ~112W peak CPU
                double cpuWatts = Math.Round(cpuBase + (cpuUsage / 100.0) * (cpuPeak - cpuBase), 1);
                double gpuWatts = Math.Round(baseline * 0.20, 1); // ~30W dedicated GPU 2D

                double totalDesktopWatts = Math.Round(platformBase + cpuWatts + gpuWatts, 1);
                snapshot.InstantDrawWatts = totalDesktopWatts;
                snapshot.IsSimulatedDraw = true;

                snapshot.PowerStatusText = $"⚡ {snapshot.InstantDrawWatts:F1} W";
                snapshot.PowerDetailTooltip = $"Desktop System Draw: {snapshot.InstantDrawWatts:F1} W (Estimated)\n├─ CPU Package: {cpuWatts:F0} W ({cpuUsage:F0}% load)\n├─ GPU (2D/Display): {gpuWatts:F0} W\n└─ Platform (Mobo/RAM/Fans): {platformBase:F0} W";
            }
        }
        else
        {
            // Fallback estimation
            double fallbackWatts = Math.Round(ConfiguredAvgWatts > 20.0 ? ConfiguredAvgWatts : 120.0, 1);
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
        double procWatts = 1.0; // Base passive background

        if (isForeground)
        {
            // Foreground active app drives the active load and platform draw (~85% of system power)
            procWatts = Math.Max(12.0, systemWatts * 0.85);
        }
        else if (hasAudio)
        {
            // Background audio/media process
            procWatts = Math.Max(3.5, systemWatts * 0.20);
        }

        PowerImpactLevel level = procWatts switch
        {
            < 10.0 => PowerImpactLevel.VeryLow,
            < 35.0 => PowerImpactLevel.Low,
            < 75.0 => PowerImpactLevel.Moderate,
            < 130.0 => PowerImpactLevel.High,
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
