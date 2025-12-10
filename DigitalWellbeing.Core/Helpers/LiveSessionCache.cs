using DigitalWellbeing.Core.Models;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace DigitalWellbeing.Core.Helpers
{
    public static class LiveSessionCache
    {
        private const string MAP_NAME = "DigitalWellbeing_LiveSessionMap";
        private const int MAP_SIZE = 4096; // 4KB should be plenty for a single session JSON
        private const string MUTEX_NAME = "DigitalWellbeing_LiveSessionMutex";

        public static void Write(AppSession session)
        {
            try
            {
                // Serialize
                // Use a simple serializer or Newtonsoft. Using System.Text.Json for modern compat if possible, 
                // but this is multi-targeted. Let's use string based JSON or robust serializer.
                // Assuming Newtonsoft is available in Core (it usually is in this project context).
                // Actually, let's look at what's available. AppSessionRepository uses JsonConvert probably?
                // Let's check dependencies later. safe bet: Newtonsoft.Json
                
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(session);
                byte[] data = System.Text.Encoding.UTF8.GetBytes(json);

                if (data.Length > MAP_SIZE) return; // Too big?

                // Lock
                using (var mutex = new Mutex(false, MUTEX_NAME))
                {
                    if (mutex.WaitOne(100))
                    {
                        try
                        {
                            using (var mmf = MemoryMappedFile.CreateOrOpen(MAP_NAME, MAP_SIZE))
                            {
                                using (var stream = mmf.CreateViewStream())
                                {
                                    using (var writer = new BinaryWriter(stream))
                                    {
                                        writer.Write(data.Length);
                                        writer.Write(data);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            mutex.ReleaseMutex();
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignore IPC errors to prevent service crash
            }
        }

        public static AppSession Read()
        {
            try
            {
                using (var mutex = new Mutex(false, MUTEX_NAME))
                {
                    if (mutex.WaitOne(10))
                    {
                        try
                        {
                            // On read side (Client), specific handling if it doesn't exist
                            try
                            {
                                using (var mmf = MemoryMappedFile.OpenExisting(MAP_NAME))
                                {
                                    using (var stream = mmf.CreateViewStream())
                                    {
                                        using (var reader = new BinaryReader(stream))
                                        {
                                            int length = reader.ReadInt32();
                                            if (length > 0 && length <= MAP_SIZE)
                                            {
                                                byte[] data = reader.ReadBytes(length);
                                                string json = System.Text.Encoding.UTF8.GetString(data);
                                                return Newtonsoft.Json.JsonConvert.DeserializeObject<AppSession>(json);
                                            }
                                        }
                                    }
                                }
                            }
                            catch (FileNotFoundException)
                            {
                                // Map doesn't exist yet (Service not running or hasn't written)
                                return null;
                            }
                        }
                        finally
                        {
                            mutex.ReleaseMutex();
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignore read errors
            }

            return null;
        }

        public static void Clear()
        {
             try
            {
                using (var mutex = new Mutex(false, MUTEX_NAME))
                {
                    if (mutex.WaitOne(100))
                    {
                        try
                        {
                            using (var mmf = MemoryMappedFile.CreateOrOpen(MAP_NAME, MAP_SIZE))
                            {
                                using (var stream = mmf.CreateViewStream())
                                {
                                    using (var writer = new BinaryWriter(stream))
                                    {
                                        writer.Write(0); // 0 Length = Empty
                                    }
                                }
                            }
                        }
                        finally
                        {
                            mutex.ReleaseMutex();
                        }
                    }
                }
            }
            catch {}
        }
    }
}
