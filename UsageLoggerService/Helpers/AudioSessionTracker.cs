#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace UsageLoggerService.Helpers
{
    public static class AudioSessionTracker
    {
        private static readonly string LogPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "digital-wellbeing", 
            "debug_audio.txt");

        private static void Log(string message) 
        { 
            try 
            { 
               System.IO.File.AppendAllText(LogPath, $"{DateTime.Now}: {message}{Environment.NewLine}"); 
            } 
            catch { } 
        }

        public static List<string> GetActiveAudioSessions()
        {
            var activeApps = new List<string>();
            
            // Run on STA thread to be safe with COM
            var thread = new System.Threading.Thread(() => 
            {
                activeApps = GetActiveAudioSessionsInternal();
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            return activeApps;
        }

        private static List<string> GetActiveAudioSessionsInternal()
        {
            var activeApps = new List<string>();
            MMDeviceEnumerator? enumerator = null;
            MMDeviceCollection? devices = null;

            try
            {
                enumerator = new MMDeviceEnumerator();
                devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                // Log($"NAudio found {devices.Count} devices.");

                foreach (var device in devices)
                {
                    try
                    {
                        // NAudio handles the Activation internally via this property
                        var sessions = device.AudioSessionManager.Sessions;
                        
                        if (sessions == null) continue;

                        for (int i = 0; i < sessions.Count; i++)
                        {
                            try
                            {
                                var session = sessions[i];
                                
                                // Log($"  Session: {session.DisplayName} State: {session.State}");
                                // Log($"  Session: {session.DisplayName} State: {session.State}");

                                if (session.State == AudioSessionState.AudioSessionStateActive)
                                {
                                    // Check if actually playing sound (Peak Value > 0)
                                    // Some apps keep session Active but Silent when paused.
                                    if (session.AudioMeterInformation.MasterPeakValue > 0)
                                    {
                                        uint pid = session.GetProcessID;
                                        if (pid > 0)
                                        {
                                            SafeAddProcessName((int)pid, activeApps);
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // Log($"  Session Error: {sEx.Message}");
                            }
                        }
                    }
                    catch
                    {
                        // Log($"  Device Error (Activation/Access): {devEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"NAudio Critical Error: {ex.Message}");
            }
            finally
            {
                // if (devices != null) devices.Dispose(); // MMDeviceCollection is not IDisposable in NAudio
                if (enumerator != null) enumerator.Dispose();
            }

            return activeApps;
        }

        private static void SafeAddProcessName(int pid, List<string> activeApps)
        {
            try
            {
                using (var p = Process.GetProcessById(pid))
                {
                    if (!string.IsNullOrEmpty(p.ProcessName) && !activeApps.Contains(p.ProcessName))
                    {
                        activeApps.Add(p.ProcessName);
                        // Log($"Found: {p.ProcessName}");
                    }
                }
            }
            catch
            {
            }
        }
        
        /// <summary>
        /// Fallback: Check if ANY audio is playing on the default render device.
        /// Returns true if global master peak > threshold (1%).
        /// </summary>
        public static bool IsGlobalAudioPlaying()
        {
            bool isPlaying = false;
            var thread = new System.Threading.Thread(() =>
            {
                isPlaying = IsGlobalAudioPlayingInternal();
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
            return isPlaying;
        }

        private static bool IsGlobalAudioPlayingInternal()
        {
            MMDeviceEnumerator? enumerator = null;
            try
            {
                enumerator = new MMDeviceEnumerator();
                var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                if (defaultDevice != null)
                {
                    float peak = defaultDevice.AudioMeterInformation.MasterPeakValue;
                    // Log($"[Fallback] Global Peak: {peak:P2}");
                    return peak > 0.01f; // 1% threshold to avoid noise
                }
            }
            catch (Exception ex)
            {
                Log($"[Fallback] Error: {ex.Message}");
            }
            finally
            {
                enumerator?.Dispose();
            }
            return false;
        }
    }
}
