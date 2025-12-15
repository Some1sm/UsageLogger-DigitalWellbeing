using DigitalWellbeing.Core.Models;
using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace DigitalWellbeing.Core.Helpers
{
    /// <summary>
    /// RAM-based IPC using Memory-Mapped Files for sharing ALL sessions between service and UI.
    /// Stores the complete session list (buffer + current) so UI gets full real-time data.
    /// Disk writes only happen every 5 minutes or on program close.
    /// </summary>
    public static class LiveSessionCache
    {
        private const string MAP_NAME = "DigitalWellbeing_LiveSessionMap_v3";
        private const int MAP_SIZE = 512 * 1024; // 512KB for multiple sessions
        private const string MUTEX_NAME = "DigitalWellbeing_LiveSessionMutex_v3";
        
        // Keep MMF alive as static field - DO NOT DISPOSE until app exits
        private static MemoryMappedFile _mmf;
        private static readonly object _lock = new object();
        
        private static MemoryMappedFile GetOrCreateMMF()
        {
            if (_mmf == null)
            {
                lock (_lock)
                {
                    if (_mmf == null)
                    {
                        _mmf = MemoryMappedFile.CreateOrOpen(MAP_NAME, MAP_SIZE);
                    }
                }
            }
            return _mmf;
        }

        /// <summary>
        /// Write ALL sessions (buffer + current) to RAM for the UI to read.
        /// </summary>
        public static void WriteAll(List<AppSession> allSessions)
        {
            if (allSessions == null) return;
            
            Mutex mutex = null;
            bool hasMutex = false;
            
            try
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(allSessions);
                byte[] data = System.Text.Encoding.UTF8.GetBytes(json);

                if (data.Length > MAP_SIZE - 4) return; // Too big

                mutex = new Mutex(false, MUTEX_NAME);
                hasMutex = mutex.WaitOne(500);
                
                if (hasMutex)
                {
                    var mmf = GetOrCreateMMF();
                    using (var accessor = mmf.CreateViewAccessor())
                    {
                        accessor.Write(0, data.Length);
                        accessor.WriteArray(4, data, 0, data.Length);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore IPC errors to prevent service crash
            }
            finally
            {
                if (hasMutex && mutex != null)
                {
                    try { mutex.ReleaseMutex(); } catch { }
                }
                if (mutex != null)
                {
                    try { mutex.Dispose(); } catch { }
                }
            }
        }

        /// <summary>
        /// Read ALL sessions from RAM (for today's real-time data).
        /// </summary>
        public static List<AppSession> ReadAll()
        {
            Mutex mutex = null;
            bool hasMutex = false;
            
            try
            {
                mutex = new Mutex(false, MUTEX_NAME);
                hasMutex = mutex.WaitOne(500);
                
                if (hasMutex)
                {
                    MemoryMappedFile mmf = null;
                    try
                    {
                        mmf = MemoryMappedFile.OpenExisting(MAP_NAME);
                    }
                    catch (System.IO.FileNotFoundException)
                    {
                        return new List<AppSession>();
                    }
                    
                    if (mmf != null)
                    {
                        using (var accessor = mmf.CreateViewAccessor())
                        {
                            int length = accessor.ReadInt32(0);
                            if (length > 0 && length < MAP_SIZE - 4)
                            {
                                byte[] data = new byte[length];
                                accessor.ReadArray(4, data, 0, length);
                                string json = System.Text.Encoding.UTF8.GetString(data);
                                return Newtonsoft.Json.JsonConvert.DeserializeObject<List<AppSession>>(json) 
                                       ?? new List<AppSession>();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignore read errors
            }
            finally
            {
                if (hasMutex && mutex != null)
                {
                    try { mutex.ReleaseMutex(); } catch { }
                }
                if (mutex != null)
                {
                    try { mutex.Dispose(); } catch { }
                }
            }

            return new List<AppSession>();
        }

        // Legacy single-session methods for compatibility
        public static void Write(AppSession session)
        {
            if (session == null) return;
            WriteAll(new List<AppSession> { session });
        }

        public static AppSession Read()
        {
            var sessions = ReadAll();
            return sessions.Count > 0 ? sessions[sessions.Count - 1] : null;
        }

        public static void Clear()
        {
            WriteAll(new List<AppSession>());
        }
    }
}
